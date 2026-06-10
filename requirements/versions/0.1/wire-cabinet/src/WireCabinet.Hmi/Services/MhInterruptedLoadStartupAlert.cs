using System.Text;
using System.Windows;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>启动时展示 MH 存料中断提醒弹窗。</summary>
public static class MhInterruptedLoadStartupAlert
{
    public static bool TryShow(Window owner)
    {
        App.Bootstrap.FlowSlots.Reload();
        TrySanitizeStaleRecord();

        var record = App.Bootstrap.InterruptedLoad.TryGet();
        if (record is null)
            return false;

        var doorStillOpen = IsSlotDoorOpen(record, out _);
        var slotLabel = MhOpenDoorGuard.FormatCabinetSlotLabel(record.SlotNo);
        var body = BuildMessage(record, slotLabel, doorStillOpen);

        MessageBox.Show(owner, body, "上次存料未完成", MessageBoxButton.OK, MessageBoxImage.Warning);

        if (!doorStillOpen)
            App.Bootstrap.InterruptedLoad.Clear();

        return true;
    }

    /// <summary>若该格口已 bind 同一批号，清除过期中断记录。</summary>
    public static void TrySanitizeStaleRecord()
    {
        var record = App.Bootstrap.InterruptedLoad.TryGet();
        if (record is null)
            return;

        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == record.SlotId);
        if (slot is null)
        {
            App.Bootstrap.InterruptedLoad.Clear();
            return;
        }

        if (string.Equals(slot.BizState, "available_wire", StringComparison.OrdinalIgnoreCase)
            && string.Equals(slot.WireLotNo, record.WireLotNo, StringComparison.OrdinalIgnoreCase))
            App.Bootstrap.InterruptedLoad.Clear();
    }

    public static bool TryGetInterruptedSlotDoorOpen(out MhInterruptedLoad? record, out bool doorStillOpen)
    {
        record = App.Bootstrap.InterruptedLoad.TryGet();
        doorStillOpen = false;
        if (record is null)
            return false;

        doorStillOpen = IsSlotDoorOpen(record, out _);
        return true;
    }

    internal static string? BuildStatusHint(MhInterruptedLoad record, bool doorStillOpen)
    {
        var slotLabel = MhOpenDoorGuard.FormatCabinetSlotLabel(record.SlotNo);
        return doorStillOpen
            ? $"上次中断存料：{slotLabel}，批号 {record.WireLotNo}，请先关好该格口。"
            : $"上次中断存料：{slotLabel}，批号 {record.WireLotNo}，请确认格内实物后重新存料。";
    }

    private static bool IsSlotDoorOpen(MhInterruptedLoad record, out SlotDoorState? slot)
    {
        slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == record.SlotId);
        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        App.SlotHardwarePoll.Snapshots.TryGetValue(record.SlotNo, out var hw);
        return slot is not null && SlotDoorDisplay.IsDisplayOpen(slot, hw, hwConfigured);
    }

    internal static string BuildMessage(MhInterruptedLoad record, string slotLabel, bool doorStillOpen)
    {
        var sb = new StringBuilder();
        sb.AppendLine("上次存放焊丝时程序异常关闭，存料流程未完成入账。");
        sb.AppendLine();
        sb.AppendLine($"格口：{slotLabel}");
        sb.AppendLine($"批号：{record.WireLotNo}");
        if (!string.IsNullOrWhiteSpace(record.WireSpec))
            sb.AppendLine($"规格：{record.WireSpec}");
        sb.AppendLine($"本地 bind：{(record.BindDone ? "已完成" : "未完成")}；MES DISCOEQPNO：{(record.MesDiscoDone ? "已同步" : "未同步")}");
        sb.AppendLine();

        if (doorStillOpen)
        {
            sb.AppendLine("该格口目前仍未关闭。");
            sb.AppendLine("请先将格口内焊丝取出（若已放入），关好格口后，重新扫码走存料流程。");
        }
        else
        {
            sb.AppendLine("该格口已关闭，但系统未记录存料完成。");
            sb.AppendLine($"请先到 {slotLabel} 确认格内是否有焊丝：");
            sb.AppendLine("· 若有，请先取出并关好格口；");
            sb.AppendLine("· 确认无误后，回到物料间重新扫码存料。");
        }

        return sb.ToString().TrimEnd();
    }
}
