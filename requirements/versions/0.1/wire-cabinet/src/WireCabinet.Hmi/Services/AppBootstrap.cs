using System.IO;
using Microsoft.Extensions.Configuration;
using WireCabinet.Core;
using WireCabinet.Core.Config;
using WireCabinet.Data;
using WireCabinet.Flows;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

public sealed class AppBootstrap : IDisposable
{
    public SqliteDb AppDb { get; }
    public SqlCatalog Catalog { get; }
    public IMesGateway Mes { get; }
    public IAppDb AppDbGateway { get; }
    public SlotIoConfig SlotIo { get; }
    public ISlotHardwareService Hardware { get; }
    public DoorOperationGate DoorOps { get; }
    public ISlotControlService SlotControl { get; }
    public ISlotController FlowSlots { get; }
    public IWireOperationSession WireSession { get; } = new WireOperationSession();
    public MhInterruptedLoadStore InterruptedLoad { get; }
    public OpInterruptedIssueStore OpInterruptedIssue { get; }
    public WireMesDiscoSyncStore MesDiscoPending { get; }
    public MesReconAuditLogStore MesReconAudit { get; }
    public WireDiscoEqpSyncService DiscoSync { get; }
    public MesDiscoReconciliationService MesReconciliation { get; }
    public WireMesInterruptGuard InterruptGuard { get; }
    public EngineServices EngineServices { get; }
    public IReadOnlyList<FlowDefinition> Flows { get; }
    public MesOptions MesOptions { get; }
    public FlowTraceWindowOptions FlowTraceOptions { get; }
    public bool MesReady { get; }
    public bool FlowTraceEnabled => FlowTraceOptions.Enabled;

    public AppBootstrap(IConfiguration config)
    {
        var dbPath = config["ApplicationDatabase:Path"];
        if (string.IsNullOrWhiteSpace(dbPath))
            dbPath = CabinetPaths.DefaultAppDbPath;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        var needsInit = !SqliteDb.HasAppSlotTable(dbPath);
        if (needsInit && File.Exists(dbPath) && new FileInfo(dbPath).Length == 0)
            File.Delete(dbPath);
        AppDb = new SqliteDb(dbPath);
        if (needsInit)
            AppDb.Initialize(recreate: false);

        WireMesDiscoSchema.EnsureAll(AppDb);

        InterruptedLoad = new MhInterruptedLoadStore(AppDb);
        InterruptedLoad.EnsureSchema();

        OpInterruptedIssue = new OpInterruptedIssueStore(AppDb);
        OpInterruptedIssue.EnsureSchema();

        MesDiscoPending = new WireMesDiscoSyncStore(AppDb);
        MesDiscoPending.EnsureSchema();

        MesReconAudit = new MesReconAuditLogStore(AppDb);
        MesReconAudit.EnsureSchema();

        Catalog = SqlCatalog.Load();
        MesOptions = new MesOptions
        {
            ConnectionString = NormalizeMesConnectionString(config["Mes:ConnectionString"]),
            MatTransWriter = config["Mes:MatTransWriter"] ?? "新厂前线物料多仓位2"
        };
        MesReady = MesOptions.IsConfigured;
        Mes = MesReady
            ? new OracleMesGateway(MesOptions.ConnectionString)
            : new SqliteMockMesGateway(AppDb, new MesFunctionSettings());

        AppDbGateway = new SqliteAppDb(AppDb);
        SlotIo = SlotIoConfig.Load();
        Hardware = new ModbusSlotHardwareService(SlotIo);
        DoorOps = new DoorOperationGate();
        SlotControl = new SlotControlService(AppDb, Hardware, DoorOps);
        FlowSlots = new FlowSlotController(SlotControl, AppDb);

        InterruptGuard = new WireMesInterruptGuard(InterruptedLoad, OpInterruptedIssue);

        DiscoSync = new WireDiscoEqpSyncService(
            Catalog,
            AppDbGateway,
            Mes,
            MesDiscoPending,
            MesOptions.MatTransWriter,
            MesReconAudit,
            InterruptGuard);

        MesReconciliation = new MesDiscoReconciliationService(
            AppDbGateway,
            Mes,
            Catalog,
            DiscoSync,
            InterruptGuard);

        EngineServices = new EngineServices
        {
            Catalog = Catalog,
            AppDb = AppDbGateway,
            Mes = Mes,
            Slot = FlowSlots,
            SystemAgvNo = config["AgvDispatch:DefaultDeviceKey"] ?? "AGV-01",
            SystemMesWriter = MesOptions.MatTransWriter,
            DiscoSync = DiscoSync
        };

        Flows = FlowLoader.LoadAll();

        FlowTraceOptions = new FlowTraceWindowOptions();
        config.GetSection("Debug:FlowTraceWindow").Bind(FlowTraceOptions);
    }

    public void Dispose() => (Hardware as IDisposable)?.Dispose();

    /// <summary>去掉从旧版配置整段粘贴时误带的 OracleConnection= 前缀。</summary>
    private static string NormalizeMesConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var s = raw.Trim();
        const string legacyPrefix = "OracleConnection=";
        if (s.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            s = s[legacyPrefix.Length..].TrimStart();

        return s;
    }
}

public sealed class UnconfiguredMesGateway : IMesGateway
{
    public MesMode Mode => MesMode.Oracle;
    public DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters) =>
        DbResult.Failure(item.OracleSql ?? item.Sql, "MES 未配置：请在 appsettings.json 填写 Mes:ConnectionString。");
}
