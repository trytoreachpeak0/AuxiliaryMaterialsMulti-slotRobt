using WireCabinet.Slots.Hardware;

namespace WireCabinet.Slots;

/// <summary>格口联锁开门判定，与 HMI <c>MhOperationLock.IsSlotBlockingOpen</c> 一致。</summary>
public static class SlotInterlockHelper
{
    public static bool IsSlotOpenForInterlock(
        SlotStatusDto slot,
        IReadOnlyDictionary<string, SlotHardwareSnapshot> snapshots,
        bool hardwareConfigured)
    {
        if (slot.DoorState == "open")
            return true;

        return hardwareConfigured
               && snapshots.TryGetValue(slot.SlotNo, out var hw)
               && hw is { ReadOk: true, LockClosed: false };
    }
}
