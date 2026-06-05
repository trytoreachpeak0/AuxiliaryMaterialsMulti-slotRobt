using WireCabinet.Data;

namespace WireCabinet.Hmi.Services;

/// <summary>MH 存料开门后、写库前异常退出时的中断快照（仅提醒，不自动 bind）。</summary>
public sealed record MhInterruptedLoad(
    long SlotId,
    string SlotNo,
    string WireLotNo,
    string? WireSpec,
    DateTime InterruptedAt);

public sealed class MhInterruptedLoadStore
{
    private const int RowId = 1;
    private const string FlowId = MhStationAccess.LoadFlowId;

    private readonly SqliteDb _db;

    public MhInterruptedLoadStore(SqliteDb db) => _db = db;

    public void EnsureSchema()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS mh_interrupted_load (
                id              INTEGER PRIMARY KEY CHECK (id = 1),
                flow_id         TEXT NOT NULL,
                slot_id         INTEGER NOT NULL,
                slot_no         TEXT NOT NULL,
                wire_lot_no     TEXT NOT NULL,
                wire_spec       TEXT,
                interrupted_at  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Save(long slotId, string slotNo, string wireLotNo, string? wireSpec)
    {
        if (slotId <= 0 || string.IsNullOrWhiteSpace(slotNo) || string.IsNullOrWhiteSpace(wireLotNo))
            return;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mh_interrupted_load (id, flow_id, slot_id, slot_no, wire_lot_no, wire_spec, interrupted_at)
            VALUES (1, $flow, $sid, $sno, $lot, $spec, datetime('now'))
            ON CONFLICT(id) DO UPDATE SET
                flow_id = excluded.flow_id,
                slot_id = excluded.slot_id,
                slot_no = excluded.slot_no,
                wire_lot_no = excluded.wire_lot_no,
                wire_spec = excluded.wire_spec,
                interrupted_at = excluded.interrupted_at;
            """;
        cmd.Parameters.AddWithValue("$flow", FlowId);
        cmd.Parameters.AddWithValue("$sid", slotId);
        cmd.Parameters.AddWithValue("$sno", slotNo.Trim());
        cmd.Parameters.AddWithValue("$lot", wireLotNo.Trim());
        cmd.Parameters.AddWithValue("$spec", string.IsNullOrWhiteSpace(wireSpec) ? DBNull.Value : wireSpec.Trim());
        cmd.ExecuteNonQuery();
    }

    public MhInterruptedLoad? TryGet()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT slot_id, slot_no, wire_lot_no, wire_spec, interrupted_at
            FROM mh_interrupted_load WHERE id = 1;
            """;
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;

        var atText = r.IsDBNull(4) ? null : r.GetString(4);
        DateTime at = DateTime.TryParse(atText, out var parsed) ? parsed : DateTime.UtcNow;

        return new MhInterruptedLoad(
            r.GetInt64(0),
            r.GetString(1),
            r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            at);
    }

    public void Clear()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM mh_interrupted_load WHERE id = 1;";
        cmd.ExecuteNonQuery();
    }
}
