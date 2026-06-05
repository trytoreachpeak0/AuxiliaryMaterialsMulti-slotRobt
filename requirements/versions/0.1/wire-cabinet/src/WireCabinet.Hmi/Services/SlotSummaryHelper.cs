using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>格口柜体统计：MH 用空闲/有料/退回；维护页用启用/禁用。</summary>
public static class SlotSummaryHelper
{
    public readonly record struct SlotSummaryCounts(int Total, int Idle, int Loaded, int Returned);

    public readonly record struct MaintSummaryCounts(int Total, int Enabled, int Disabled);

    public static MaintSummaryCounts ComputeMaint(IReadOnlyList<SlotDoorState> all)
    {
        var total = all.Count;
        var enabled = all.Count(s => s.IsEnabled);
        return new MaintSummaryCounts(total, enabled, total - enabled);
    }

    public static SlotSummaryCounts Compute(
        IReadOnlyList<SlotDoorState> all,
        IReadOnlyDictionary<string, SlotHardwareSnapshot> hwSnapshots,
        bool hardwareConfigured)
    {
        var total = all.Count;
        var idle = all.Count(s =>
            s.BizState == "idle" &&
            !SlotDoorDisplay.IsDisplayOpen(s, TryHw(hwSnapshots, s.SlotNo), hardwareConfigured));
        var loaded = all.Count(s => s.BizState == "available_wire");
        var returned = all.Count(s => s.BizState == "returned_wire");
        return new SlotSummaryCounts(total, idle, loaded, returned);
    }

    private static SlotHardwareSnapshot? TryHw(
        IReadOnlyDictionary<string, SlotHardwareSnapshot> snapshots,
        string slotNo)
    {
        snapshots.TryGetValue(slotNo, out var hw);
        return hw;
    }
}
