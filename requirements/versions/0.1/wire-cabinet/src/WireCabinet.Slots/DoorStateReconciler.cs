using WireCabinet.Slots.Hardware;

namespace WireCabinet.Slots;

/// <summary>根据锁 DI 将仍为 open 的格口回写为 closed（设计：DI 先 Locked，再更新库）。</summary>
public sealed class DoorStateReconciler
{
    private readonly ISlotControlService _slots;
    private readonly ISlotHardwareService _hardware;

    public DoorStateReconciler(ISlotControlService slots, ISlotHardwareService hardware)
    {
        _slots = slots;
        _hardware = hardware;
    }

    /// <summary>对库 open 且 DI 锁闭合的格口执行 ConfirmDoorClosed。返回成功关库的 slot_id。</summary>
    public async Task<IReadOnlyList<long>> ReconcileAsync(
        IReadOnlyDictionary<string, SlotHardwareSnapshot> snapshots,
        CancellationToken ct = default)
    {
        if (!_hardware.IsConfigured)
            return [];

        var openIds = _slots.OpenSlotIds();
        if (openIds.Count == 0)
            return [];

        var slots = await _slots.ListSlotsAsync(new SlotListFilter { EnabledOnly = false }, ct);
        var closed = new List<long>();

        foreach (var id in openIds)
        {
            var row = slots.FirstOrDefault(s => s.SlotId == id);
            if (row is null || row.DoorState != "open")
                continue;

            if (!snapshots.TryGetValue(row.SlotNo, out var hw)
                || !hw.ReadOk
                || hw.LockClosed != true)
                continue;

            if (await _slots.ConfirmDoorClosedAsync(id, ct))
                closed.Add(id);
        }

        return closed;
    }
}
