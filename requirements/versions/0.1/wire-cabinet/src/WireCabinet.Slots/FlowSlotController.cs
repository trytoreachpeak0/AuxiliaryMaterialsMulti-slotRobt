namespace WireCabinet.Slots;

/// <summary>将 <see cref="ISlotControlService"/> 适配为流程引擎 <see cref="ISlotController"/>。</summary>
public sealed class FlowSlotController : ISlotController
{
    private readonly ISlotControlService _control;
    private readonly WireCabinet.Data.SqliteDb _db;
    private readonly List<SlotDoorState> _slots = new();

    public FlowSlotController(ISlotControlService control, WireCabinet.Data.SqliteDb db)
    {
        _control = control;
        _db = db;
        Reload();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SlotDoorState> Slots => _slots;

    public void Reload()
    {
        _slots.Clear();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT slot_id, slot_no, usage_type, biz_state, door_state, lock_state, is_enabled, wire_lot_no, wire_spec
            FROM app_slot ORDER BY slot_index, slot_no
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            _slots.Add(new SlotDoorState
            {
                SlotId = reader.GetInt64(0),
                SlotNo = reader.GetString(1),
                UsageType = reader.GetString(2),
                BizState = reader.GetString(3),
                IsOpen = reader.GetString(4) == "open",
                IsLocked = reader.GetString(5) == "locked",
                IsEnabled = reader.GetInt64(6) == 1,
                WireLotNo = reader.IsDBNull(7) ? null : reader.GetString(7),
                WireSpec = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> OpenDoorAsync(long slotId, CancellationToken ct = default)
    {
        var slot = _slots.FirstOrDefault(s => s.SlotId == slotId);
        var result = await _control.OpenSlotAsync(new OpenSlotRequest
        {
            SlotId = slotId,
            SlotNo = slot?.SlotNo,
            Source = "flow",
            OperationLabel = "业务流程开门"
        }, ct);
        Reload();
        return result.Success;
    }

    public async Task CloseDoorAsync(long slotId, CancellationToken ct = default)
    {
        await _control.ConfirmDoorClosedAsync(slotId, ct);
        Reload();
    }

    public void OpenDoor(long slotId) => OpenDoorAsync(slotId).GetAwaiter().GetResult();

    public void CloseDoor(long slotId) => CloseDoorAsync(slotId).GetAwaiter().GetResult();

    public long? CloseNextOpenDoor()
    {
        var slot = _slots.FirstOrDefault(s => s.IsOpen);
        if (slot is null) return null;
        CloseDoor(slot.SlotId);
        return slot.SlotId;
    }

    public bool AreAllDoorsClosed() => _control.AreAllDoorsClosed();

    public IReadOnlyList<long> OpenDoorIds() => _control.OpenSlotIds();

}
