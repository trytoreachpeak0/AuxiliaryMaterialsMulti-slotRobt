using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WireCabinet.Data;
using WireCabinet.Hmi.Services;

namespace WireCabinet.Hmi.Views;

public partial class MesReconView : UserControl
{
    public MesReconView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshAll();
    }

    private WireDiscoEqpSyncService Sync => App.Bootstrap.DiscoSync;
    private MesDiscoReconciliationService Reconcile => App.Bootstrap.MesReconciliation;

    private void RefreshAll()
    {
        VehicleNameText.Text = $"当前小车名（DISCOEQPNO）：{Sync.DiscoEqpNo}";
        RefreshPending();
        RefreshDrift();
        RefreshInterrupts();
        RefreshAudit();
    }

    private void RefreshPending()
    {
        PendingList.ItemsSource = Sync.ListPending().Select(PendingVm.From).ToList();
    }

    private void RefreshDrift()
    {
        DriftList.ItemsSource = Reconcile.ComputeDrift().Select(DriftVm.From).ToList();
    }

    private void RefreshInterrupts()
    {
        var sb = new StringBuilder();
        var mh = App.Bootstrap.InterruptedLoad.TryGet();
        if (mh is not null)
        {
            sb.AppendLine("[MH 存料中断]");
            sb.AppendLine($"格口 {mh.SlotNo}，批号 {mh.WireLotNo}");
            sb.AppendLine($"bind_done={mh.BindDone}，mes_disco_done={mh.MesDiscoDone}");
            sb.AppendLine("建议：确认实物后重新扫码存料，或手工处理后清除快照。");
            sb.AppendLine();
        }

        var op = App.Bootstrap.OpInterruptedIssue.TryGet();
        if (op is not null)
        {
            sb.AppendLine("[OP 领用中断]");
            sb.AppendLine($"格口 {op.SlotNo}，批号 {op.WireLotNo}");
            sb.AppendLine($"submit_issue_done={op.SubmitIssueDone}，pickup_done={op.PickupDone}，mes_disco_done={op.MesDiscoDone}");
            sb.AppendLine("建议：重新走 OP 或手工补 NULL/清库后清除快照。");
        }

        InterruptText.Text = sb.Length == 0 ? "（无中断快照）" : sb.ToString().TrimEnd();
    }

    private void RefreshAudit()
    {
        AuditList.ItemsSource = App.Bootstrap.MesReconAudit.ListRecent(30)
            .Select(a => new AuditVm
            {
                TimeText = a.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Action = a.Action,
                WireLotNo = a.WireLotNo ?? "",
                Detail = a.Detail
            }).ToList();
    }

    private void RefreshPending_Click(object sender, RoutedEventArgs e) => RefreshPending();

    private void RefreshDrift_Click(object sender, RoutedEventArgs e) => RefreshDrift();

    private void RetrySelectedPending_Click(object sender, RoutedEventArgs e)
    {
        if (PendingList.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择 pending 记录。", "MES 对账", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (PendingVm item in PendingList.SelectedItems)
        {
            var result = Sync.RetryPendingRow(item.Id, manual: true);
            if (!result.Success)
                MessageBox.Show($"批号 {item.WireLotNo}：{result.UserMessage}", "重试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshAll();
    }

    private void MarkManualResolved_Click(object sender, RoutedEventArgs e)
    {
        if (PendingList.SelectedItem is not PendingVm item)
        {
            MessageBox.Show("请选择一条 pending 记录。", "MES 对账", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确认批号 {item.WireLotNo} 已人工处理？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Sync.MarkManualResolved(item.Id);
        RefreshAll();
    }

    private void EnqueueDriftRepair_Click(object sender, RoutedEventArgs e)
    {
        var count = Reconcile.EnqueueAutoRepairForDrift();
        MessageBox.Show($"已处理 {count} 条非中断漂移项（成功或已写入 pending）。", "MES 对账", MessageBoxButton.OK, MessageBoxImage.Information);
        RefreshAll();
    }

    private void ClearMhSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (App.Bootstrap.InterruptedLoad.TryGet() is null)
            return;
        if (MessageBox.Show("确认 MH 中断已人工核对并清除快照？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        App.Bootstrap.MesReconAudit.Append("clear_mh_snapshot", null, "人工清除 MH 中断快照");
        App.Bootstrap.InterruptedLoad.Clear();
        RefreshInterrupts();
        RefreshAudit();
    }

    private void ClearOpSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (App.Bootstrap.OpInterruptedIssue.TryGet() is null)
            return;
        if (MessageBox.Show("确认 OP 中断已人工核对并清除快照？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        App.Bootstrap.MesReconAudit.Append("clear_op_snapshot", null, "人工清除 OP 中断快照");
        App.Bootstrap.OpInterruptedIssue.Clear();
        RefreshInterrupts();
        RefreshAudit();
    }

    private void ManualSet_Click(object sender, RoutedEventArgs e)
    {
        var lot = ManualLotBox.Text.Trim();
        if (string.IsNullOrEmpty(lot))
            return;
        var result = Sync.ManualSet(lot);
        MessageBox.Show(result.UserMessage, "手工 SET", MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        RefreshAll();
    }

    private void ManualClear_Click(object sender, RoutedEventArgs e)
    {
        var lot = ManualLotBox.Text.Trim();
        if (string.IsNullOrEmpty(lot))
            return;
        var result = Sync.ManualClear(lot);
        MessageBox.Show(result.UserMessage, "手工 NULL", MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        RefreshAll();
    }

    private sealed class PendingVm
    {
        public long Id { get; init; }
        public string WireLotNo { get; init; } = "";
        public string ActionText { get; init; } = "";
        public string StatusText { get; init; } = "";
        public string KindText { get; init; } = "";
        public int RetryCount { get; init; }
        public string? LastError { get; init; }

        public static PendingVm From(WireMesDiscoPendingRow row) => new()
        {
            Id = row.Id,
            WireLotNo = row.WireLotNo,
            ActionText = row.Action == WireMesDiscoAction.Set ? "SET" : "CLEAR",
            StatusText = row.Status.ToString(),
            KindText = row.FailureKind == WireMesDiscoFailureKind.Inline ? "inline" : "reconcile",
            RetryCount = row.RetryCount,
            LastError = row.LastError
        };
    }

    private sealed class DriftVm
    {
        public string WireLotNo { get; init; } = "";
        public string DriftType { get; init; } = "";
        public string Hint { get; init; } = "";

        public static DriftVm From(MesDiscoDriftItem item) => new()
        {
            WireLotNo = item.WireLotNo,
            DriftType = item.OnlyInCabinet ? "仅柜内" : item.OnlyInMes ? "仅MES" : "—",
            Hint = item.Hint
        };
    }

    private sealed class AuditVm
    {
        public string TimeText { get; init; } = "";
        public string Action { get; init; } = "";
        public string WireLotNo { get; init; } = "";
        public string Detail { get; init; } = "";
    }
}
