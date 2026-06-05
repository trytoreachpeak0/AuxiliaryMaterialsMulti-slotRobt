using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>
/// MH 界面操作互斥：存料 / 批量开门 / 单格开门，须等本次打开的所有格口关闭后才能开始新动作。
/// </summary>
public static class MhOperationLock
{
    public sealed record BlockState(bool IsBlocked, string Message, IReadOnlyList<string> OpenSlotLabels);

    /// <summary>轮询/刷新前：同步开门会话追踪并清除无 runner 的残留存料会话。</summary>
    public static void ReconcileSessions(bool reloadDb = true)
    {
        App.Flows.ClearStaleLoadSession();

        if (reloadDb)
        {
            try
            {
                App.Bootstrap.FlowSlots.Reload();
            }
            catch
            {
                // ignore
            }
        }

        App.MhDoors.ReconcileSessionTracking(
            App.Bootstrap.FlowSlots.Slots,
            App.SlotHardwarePoll.Snapshots,
            App.Bootstrap.Hardware.IsConfigured);
    }

    /// <summary>评估是否应禁止新的 MH 格口相关操作（存料、批量、单开）。</summary>
    public static BlockState Evaluate(bool reloadDb = true)
    {
        ReconcileSessions(reloadDb);

        var slots = App.Bootstrap.FlowSlots.Slots;
        var snapshots = App.SlotHardwarePoll.Snapshots;
        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;

        var labels = CollectOpenLabels(slots, snapshots, hwConfigured);
        if (labels.Count > 0)
        {
            var list = string.Join("、", labels);
            return new BlockState(
                true,
                $"检测到有格口未关闭（{list}），请先关闭所有格口后再操作。",
                labels);
        }

        if (App.MhDoors.HasOpenedSlotsAwaitingClose)
        {
            return new BlockState(
                true,
                "请先关闭本次开门操作打开的所有格口后再继续。",
                []);
        }

        if (App.MhDoors.HasActiveDoorOnlySession)
        {
            return new BlockState(
                true,
                "格口开门会话进行中，请先关闭所有已开格口后再操作。",
                []);
        }

        return new BlockState(false, "", []);
    }

    /// <summary>格口是否处于「须拦截新操作」的打开态：库 open 或锁 DI 释放，取并集。</summary>
    public static bool IsSlotBlockingOpen(
        SlotDoorState state,
        SlotHardwareSnapshot? hw,
        bool hardwareConfigured)
    {
        if (state.IsOpen)
            return true;

        return hardwareConfigured
               && hw is { ReadOk: true, LockClosed: false };
    }

    private static List<string> CollectOpenLabels(
        IReadOnlyList<SlotDoorState> slots,
        IReadOnlyDictionary<string, SlotHardwareSnapshot> snapshots,
        bool hardwareConfigured)
    {
        var labels = new List<string>();
        foreach (var slot in slots)
        {
            snapshots.TryGetValue(slot.SlotNo, out var hw);
            if (!IsSlotBlockingOpen(slot, hw, hardwareConfigured))
                continue;
            labels.Add(MhOpenDoorGuard.FormatCabinetSlotLabel(slot.SlotNo));
        }

        return labels;
    }
}
