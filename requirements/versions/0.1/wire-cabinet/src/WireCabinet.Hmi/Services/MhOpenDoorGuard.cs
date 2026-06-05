using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

public sealed record MhOpenDoorStatus(bool HasOpenDoors, IReadOnlyList<string> OpenSlotLabels, string Message);

/// <summary>检测 MH 存料前是否仍有格口未关（库表 + 硬件 DI，与 SlotDoorDisplay 一致）。</summary>
public static class MhOpenDoorGuard
{
    public static MhOpenDoorStatus Evaluate(
        IReadOnlyList<SlotDoorState> slots,
        IReadOnlyDictionary<string, SlotHardwareSnapshot> snapshots,
        bool hardwareConfigured)
    {
        var labels = new List<string>();
        foreach (var slot in slots)
        {
            snapshots.TryGetValue(slot.SlotNo, out var hw);
            if (!MhOperationLock.IsSlotBlockingOpen(slot, hw, hardwareConfigured))
                continue;
            labels.Add(FormatCabinetSlotLabel(slot.SlotNo));
        }

        if (labels.Count == 0)
            return new MhOpenDoorStatus(false, [], "");

        var slotList = string.Join("、", labels);
        var message = $"检测到有格口未关闭（{slotList}），请先关闭所有格口后再操作。";

        return new MhOpenDoorStatus(true, labels, message);
    }

    public static string FormatCabinetSlotLabel(string slotNo)
    {
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return $"前柜 {slotNo}";
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return $"后柜 {slotNo}";
        return slotNo;
    }
}
