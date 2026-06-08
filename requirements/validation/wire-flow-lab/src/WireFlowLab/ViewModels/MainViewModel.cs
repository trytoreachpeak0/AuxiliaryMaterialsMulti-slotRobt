using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireFlowLab.Data;
using WireFlowLab.Flows;
using WireFlowLab.Infrastructure;
using WireFlowLab.SlotControl;
using WireFlowLab.Validation;

namespace WireFlowLab.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SqliteDb _db;
    private readonly SqlCatalog _catalog;
    private readonly MesFunctionSettings _mesSettings = new();
    private readonly MockSlotController _slot;
    private readonly EngineServices _svc;
    private FlowEngine _engine;

    public ObservableCollection<FlowDefinition> Flows { get; } = new();
    public ObservableCollection<TraceEntry> Trace { get; } = new();
    public ObservableCollection<Finding> Findings { get; } = new();
    public ObservableCollection<SlotTileViewModel> Slots { get; } = new();
    public ObservableCollection<InputFieldViewModel> CurrentInputs { get; } = new();

    [ObservableProperty] private FlowDefinition? _selectedFlow;
    [ObservableProperty] private string _currentNodeId = "(未开始)";
    [ObservableProperty] private string _currentNodeText = "";
    [ObservableProperty] private string _currentNodeType = "";
    [ObservableProperty] private bool _awaitingInput;
    [ObservableProperty] private string _statusText = "就绪。请选择流程并点击「开始」。";

    // MES 设置
    [ObservableProperty] private bool _useOracle;
    [ObservableProperty] private string _oracleConnString = "User Id=mes;Password=***;Data Source=host:1521/ORCLPDB";
    [ObservableProperty] private double _quotaDiff;
    [ObservableProperty] private string _quotaError = "";
    [ObservableProperty] private string _submitResult = "SUCCESS";
    [ObservableProperty] private string _agvNo = "AGV-01";
    [ObservableProperty] private string _mesModeText = "当前 MES 模式: SQLite mock";

    public MainViewModel()
    {
        _db = new SqliteDb(LabPaths.AppDbPath);
        _db.Initialize(recreate: true);
        _catalog = SqlCatalog.Load();
        _slot = new MockSlotController(_db);
        _slot.Changed += (_, _) => RefreshSlots();

        _svc = new EngineServices
        {
            Catalog = _catalog,
            AppDb = new SqliteAppDb(_db),
            Mes = new SqliteMockMesGateway(_db, _mesSettings),
            Slot = _slot,
            SystemAgvNo = _agvNo,
            SystemMesWriter = "新厂前线物料多仓位2"
        };
        _engine = new FlowEngine(_svc);

        foreach (var f in FlowLoader.LoadAll()) Flows.Add(f);
        SelectedFlow = Flows.FirstOrDefault();
        RefreshSlots();
    }

    // ---------- 命令 ----------

    [RelayCommand]
    private void StartFlow()
    {
        if (SelectedFlow is null) { StatusText = "请先选择一个流程。"; return; }

        _db.Initialize(recreate: true);
        _slot.Reload();
        Trace.Clear();
        Findings.Clear();

        foreach (var f in FlowValidator.Validate(SelectedFlow, _catalog)) Findings.Add(f);

        _engine = new FlowEngine(_svc);
        _engine.Begin(SelectedFlow);

        foreach (var (id, node) in SelectedFlow.Nodes)
            if (node.Type == NodeType.UserInput)
            {
                var map = new Dictionary<string, string>();
                foreach (var o in node.Outputs) map[o.Name] = o.Default;
                _engine.Context!.UserInputs[id] = map;
            }

        RefreshSlots();
        UpdateCurrent();
        StatusText = $"已开始流程「{SelectedFlow.Title}」。静态校验 {Findings.Count} 条。";
    }

    [RelayCommand]
    private void Step()
    {
        if (_engine.Flow is null || _engine.Finished) { StatusText = "流程未开始或已结束。"; return; }

        var node = _engine.CurrentNode;
        if (node is { Type: NodeType.UserInput })
        {
            var map = _engine.Context!.UserInputs.TryGetValue(node.Id, out var m) ? m : new Dictionary<string, string>();
            foreach (var f in CurrentInputs) map[f.Name] = f.Value;
            _engine.Context!.UserInputs[node.Id] = map;
        }

        var entry = _engine.StepOnce();
        Trace.Add(entry);
        foreach (var f in entry.Findings) Findings.Add(f);
        RefreshSlots();
        UpdateCurrent();

        if (_engine.Finished)
            StatusText = $"流程结束于「{entry.NodeId}」({entry.Outcome})。";
    }

    [RelayCommand]
    private void RunToEnd()
    {
        if (_engine.Flow is null) { StatusText = "请先开始流程。"; return; }
        var guard = 0;
        while (!_engine.Finished && guard++ < 300)
        {
            var node = _engine.CurrentNode;
            if (node is { Type: NodeType.UserInput })
            {
                var map = _engine.Context!.UserInputs.TryGetValue(node.Id, out var m) ? m : new Dictionary<string, string>();
                if (CurrentInputs.Count > 0 && string.Equals(CurrentNodeId, node.Id))
                    foreach (var f in CurrentInputs) map[f.Name] = f.Value;
                _engine.Context!.UserInputs[node.Id] = map;
            }
            var entry = _engine.StepOnce();
            Trace.Add(entry);
            foreach (var f in entry.Findings) Findings.Add(f);
        }
        RefreshSlots();
        UpdateCurrent();
        StatusText = _engine.Finished ? "连续执行完成。" : "达到步数上限，已停止。";
    }

    [RelayCommand]
    private void ResetDb()
    {
        _db.Initialize(recreate: true);
        _slot.Reload();
        Trace.Clear();
        StatusText = "数据库已重置并重新灌入种子数据。";
    }

    [RelayCommand]
    private void ApplyMes()
    {
        _svc.SystemAgvNo = AgvNo;
        if (UseOracle)
        {
            _svc.Mes = new OracleMesGateway(OracleConnString);
            MesModeText = "当前 MES 模式: Oracle（真实 MES）";
        }
        else
        {
            _mesSettings.QuotaDiff = QuotaDiff;
            _mesSettings.QuotaError = QuotaError;
            _mesSettings.SubmitResult = SubmitResult;
            _svc.Mes = new SqliteMockMesGateway(_db, _mesSettings);
            MesModeText = "当前 MES 模式: SQLite mock";
        }
        StatusText = $"MES 设置已应用。{MesModeText}";
    }

    [RelayCommand]
    private void OpenDoor(SlotTileViewModel? tile)
    {
        if (tile is null) return;
        _slot.OpenDoor(tile.SlotId);
        StatusText = $"手动打开格口 {tile.SlotNo}。";
    }

    [RelayCommand]
    private void CloseDoor(SlotTileViewModel? tile)
    {
        if (tile is null) return;
        _slot.CloseDoor(tile.SlotId);
        StatusText = $"手动关闭格口 {tile.SlotNo}。";
    }

    [RelayCommand]
    private void ValidateAll()
    {
        Findings.Clear();
        foreach (var flow in Flows)
            foreach (var f in FlowValidator.Validate(flow, _catalog))
                Findings.Add(f);
        StatusText = $"全部流程静态校验完成，共 {Findings.Count} 条。";
    }

    [RelayCommand]
    private void ExportFindings()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# findings — 焊丝发放流程验证结论");
        sb.AppendLine();
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        foreach (var grp in Findings.GroupBy(f => f.Severity).OrderByDescending(g => g.Key))
        {
            sb.AppendLine($"## {grp.Key} ({grp.Count()})");
            sb.AppendLine();
            foreach (var f in grp)
                sb.AppendLine($"- **{f.FlowId} / {f.NodeId}** [{f.Category}]: {f.Message}");
            sb.AppendLine();
        }
        var path = Path.Combine(LabPaths.DbDir, "findings-export.md");
        File.WriteAllText(path, sb.ToString());
        StatusText = $"已导出 {Findings.Count} 条结论到 {path}";
    }

    [RelayCommand]
    private void RunSelfTest()
    {
        var path = SelfTestRunner.Run();
        _db.Initialize(recreate: true);
        _slot.Reload();
        RefreshSlots();
        StatusText = $"自测完成，输出: {path}";
    }

    // ---------- 内部 ----------

    private void UpdateCurrent()
    {
        var node = _engine.CurrentNode;
        if (_engine.Finished || node is null)
        {
            CurrentNodeId = "(已结束)";
            CurrentNodeText = "";
            CurrentNodeType = "";
            AwaitingInput = false;
            CurrentInputs.Clear();
            return;
        }

        CurrentNodeId = node.Id;
        CurrentNodeText = node.Text;
        CurrentNodeType = node.RawType;
        AwaitingInput = node.Type == NodeType.UserInput;

        CurrentInputs.Clear();
        if (AwaitingInput)
        {
            _engine.Context!.UserInputs.TryGetValue(node.Id, out var existing);
            foreach (var o in node.Outputs)
            {
                var val = existing is not null && existing.TryGetValue(o.Name, out var v) ? v : o.Default;
                CurrentInputs.Add(new InputFieldViewModel { Name = o.Name, Label = string.IsNullOrEmpty(o.Label) ? o.Name : o.Label, Numeric = o.Numeric, Value = val });
            }
        }
    }

    private void RefreshSlots()
    {
        Slots.Clear();
        foreach (var s in _slot.Slots)
            Slots.Add(new SlotTileViewModel(s));
    }
}
