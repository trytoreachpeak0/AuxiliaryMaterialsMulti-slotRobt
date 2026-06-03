using Microsoft.Data.Sqlite;
using WireFlowLab.Data;

namespace WireFlowLab.SlotControl;

public sealed class SlotDoorState
{
    public long SlotId { get; init; }
    public string SlotNo { get; set; } = "";
    public string UsageType { get; set; } = "";
    public string BizState { get; set; } = "";
    public bool IsOpen { get; set; }
    public bool IsLocked { get; set; } = true;
    public string? WireLotNo { get; set; }
    public string? WireSpec { get; set; }
}

/// <summary>格口仓门控制接口（mock 模拟）。门状态会回写到 app_slot.door_state。</summary>
public interface ISlotController
{
    event EventHandler? Changed;
    IReadOnlyList<SlotDoorState> Slots { get; }

    void Reload();
    void OpenDoor(long slotId);
    void CloseDoor(long slotId);
    /// <summary>关闭下一个仍处于打开状态的门，返回被关闭的格口 id（无则 null）。</summary>
    long? CloseNextOpenDoor();
    bool AreAllDoorsClosed();
    IReadOnlyList<long> OpenDoorIds();
}

/// <summary>基于 SQLite app_slot 的格口门 mock 控制器，作为门状态的事实来源并回写数据库。</summary>
public sealed class MockSlotController : ISlotController
{
    private readonly SqliteDb _db;
    private readonly List<SlotDoorState> _slots = new();

    public MockSlotController(SqliteDb db)
    {
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
        cmd.CommandText = "SELECT slot_id, slot_no, usage_type, biz_state, door_state, lock_state, wire_lot_no, wire_spec FROM app_slot ORDER BY slot_no";
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
                WireLotNo = reader.IsDBNull(6) ? null : reader.GetString(6),
                WireSpec = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void OpenDoor(long slotId) => SetDoor(slotId, open: true);

    public void CloseDoor(long slotId) => SetDoor(slotId, open: false);

    public long? CloseNextOpenDoor()
    {
        var slot = _slots.FirstOrDefault(s => s.IsOpen);
        if (slot is null) return null;
        SetDoor(slot.SlotId, open: false);
        return slot.SlotId;
    }

    public bool AreAllDoorsClosed() => _slots.All(s => !s.IsOpen);

    public IReadOnlyList<long> OpenDoorIds() => _slots.Where(s => s.IsOpen).Select(s => s.SlotId).ToList();

    private void SetDoor(long slotId, bool open)
    {
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE app_slot SET door_state=$d, lock_state=$l, updated_at=datetime('now') WHERE slot_id=$id";
            cmd.Parameters.AddWithValue("$d", open ? "open" : "closed");
            cmd.Parameters.AddWithValue("$l", open ? "unlocked" : "locked");
            cmd.Parameters.AddWithValue("$id", slotId);
            cmd.ExecuteNonQuery();
        }

        var s = _slots.FirstOrDefault(x => x.SlotId == slotId);
        if (s is not null)
        {
            s.IsOpen = open;
            s.IsLocked = !open;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
