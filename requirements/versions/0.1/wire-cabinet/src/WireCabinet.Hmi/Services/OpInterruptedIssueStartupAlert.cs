using System.Text;
using System.Windows;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

using WireCabinet.Data;

namespace WireCabinet.Hmi.Services;

/// <summary>启动时展示 OP 领用中断提醒弹窗。</summary>
public static class OpInterruptedIssueStartupAlert
{
    public static bool TryShow(Window owner)
    {
        App.Bootstrap.FlowSlots.Reload();

        var record = App.Bootstrap.OpInterruptedIssue.TryGet();
        if (record is null)
            return false;

        var doorStillOpen = IsSlotDoorOpen(record, out _);
        var slotLabel = MhOpenDoorGuard.FormatCabinetSlotLabel(record.SlotNo);
        var body = BuildMessage(record, slotLabel, doorStillOpen);

        MessageBox.Show(owner, body, "上次领用未完成", MessageBoxButton.OK, MessageBoxImage.Warning);

        if (!doorStillOpen)
            App.Bootstrap.OpInterruptedIssue.Clear();

        return true;
    }

    private static bool IsSlotDoorOpen(OpInterruptedIssue record, out SlotDoorState? slot)
    {
        slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == record.SlotId);
        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        App.SlotHardwarePoll.Snapshots.TryGetValue(record.SlotNo, out var hw);
        return slot is not null && SlotDoorDisplay.IsDisplayOpen(slot, hw, hwConfigured);
    }

    private static string BuildMessage(OpInterruptedIssue record, string slotLabel, bool doorStillOpen)
    {
        var sb = new StringBuilder();
        sb.AppendLine("上次领用焊丝时程序异常关闭，领用流程未完成。");
        sb.AppendLine();
        sb.AppendLine($"格口：{slotLabel}");
        sb.AppendLine($"批号：{record.WireLotNo}");
        sb.AppendLine($"MES 领用提交：{(record.SubmitIssueDone ? "已完成" : "未完成")}");
        sb.AppendLine($"本地清库：{(record.PickupDone ? "已完成" : "未完成")}");
        sb.AppendLine($"MES DISCOEQPNO 清除：{(record.MesDiscoDone ? "已完成" : "未完成")}");
        sb.AppendLine();

        if (doorStillOpen)
        {
            sb.AppendLine("该格口目前仍未关闭。");
            sb.AppendLine("请确认是否已取丝，关好格口后重新走 OP 领用流程。");
            sb.AppendLine("程序不会自动完成清库或 MES 清除。");
        }
        else
        {
            sb.AppendLine("该格口已关闭，但系统未记录领用完成。");
            sb.AppendLine("请到格口核对实物后，重新走 OP 流程或到「MES 对账」界面人工处理。");
            if (record.SubmitIssueDone && !record.PickupDone)
                sb.AppendLine("注意：MES 领用已提交但本地未清库，重走 OP 可能冲突，建议到 MES 对账界面处理。");
        }

        return sb.ToString().TrimEnd();
    }
}
