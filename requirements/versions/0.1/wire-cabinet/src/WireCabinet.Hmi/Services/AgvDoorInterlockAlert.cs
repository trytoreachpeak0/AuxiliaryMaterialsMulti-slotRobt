using System.Text;
using System.Windows;
using WireCabinet.Hmi.Views;

namespace WireCabinet.Hmi.Services;

/// <summary>AGV 格口联锁弹窗：下单前拦截与任务中暂停通知。</summary>
public static class AgvDoorInterlockAlert
{
    public static MhOpenDoorStatus EvaluateBeforeDispatch()
    {
        App.Bootstrap.FlowSlots.Reload();
        return MhOpenDoorGuard.Evaluate(
            App.Bootstrap.FlowSlots.Slots,
            App.SlotHardwarePoll.Snapshots,
            App.Bootstrap.Hardware.IsConfigured);
    }

    public static string BuildDispatchBlockedMessage(IReadOnlyList<string> slotNosOrLabels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("检测到有格口未关闭，无法下发移动/充电任务：");
        AppendSlotBullets(sb, slotNosOrLabels);
        sb.Append("请关闭上述格口后重试。");
        return sb.ToString().TrimEnd();
    }

    public static string BuildPauseMessage(IReadOnlyList<string> slotNosOrLabels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AGV 任务已暂停：检测到格口意外打开。");
        sb.AppendLine("请关闭以下格口，关闭后任务将自动继续：");
        AppendSlotBullets(sb, slotNosOrLabels);
        return sb.ToString().TrimEnd();
    }

    public static IReadOnlyList<string> FormatSlotLabels(IEnumerable<string> slotNos) =>
        slotNos.Select(MhOpenDoorGuard.FormatCabinetSlotLabel).ToList();

    private static void AppendSlotBullets(StringBuilder sb, IReadOnlyList<string> items)
    {
        foreach (var item in items)
            sb.AppendLine($"· {item}");
    }
}

/// <summary>任务执行中格口联锁暂停弹窗（去重，避免轮询重复弹出）。</summary>
public sealed class AgvDoorInterlockNotifier
{
    private bool _pauseDialogShown;

    public void OnTick(Window owner, DoorInterlockTickResult tick)
    {
        if (string.Equals(tick.ExecutedAction, "ContinueMovement", StringComparison.Ordinal)
            || (tick.OpenSlotNos.Count == 0 && _pauseDialogShown))
        {
            _pauseDialogShown = false;
            return;
        }

        if (!string.Equals(tick.ExecutedAction, "PauseMovement", StringComparison.Ordinal))
            return;

        if (_pauseDialogShown)
            return;

        var labels = AgvDoorInterlockAlert.FormatSlotLabels(tick.OpenSlotNos);
        if (labels.Count == 0)
            return;

        _pauseDialogShown = true;
        FlowErrorDialog.Show(owner, AgvDoorInterlockAlert.BuildPauseMessage(labels));
    }

    public void Reset() => _pauseDialogShown = false;
}

public sealed record DoorInterlockTickResult(
    string? StatusMessage,
    string? ExecutedAction,
    IReadOnlyList<string> OpenSlotNos);
