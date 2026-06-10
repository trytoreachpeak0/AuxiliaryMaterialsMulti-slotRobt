using WireCabinet.Data;

namespace WireCabinet.Hmi.Services;

/// <summary>MH 存料开门后、写库前异常退出时的中断快照（仅提醒，不自动 bind）。</summary>
public sealed record MhInterruptedLoad(
    long SlotId,
    string SlotNo,
    string WireLotNo,
    string? WireSpec,
    bool BindDone,
    bool MesDiscoDone,
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
                bind_done       INTEGER NOT NULL DEFAULT 0,
                mes_disco_done  INTEGER NOT NULL DEFAULT 0,
                interrupted_at  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();

        EnsureColumn(conn, "mh_interrupted_load", "bind_done", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(conn, "mh_interrupted_load", "mes_disco_done", "INTEGER NOT NULL DEFAULT 0");
    }

    public void Save(long slotId, string slotNo, string wireLotNo, string? wireSpec)
    {
        if (slotId <= 0 || string.IsNullOrWhiteSpace(slotNo) || string.IsNullOrWhiteSpace(wireLotNo))
            return;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mh_interrupted_load (id, flow_id, slot_id, slot_no, wire_lot_no, wire_spec, bind_done, mes_disco_done, interrupted_at)
            VALUES (1, $flow, $sid, $sno, $lot, $spec, 0, 0, datetime('now'))
            ON CONFLICT(id) DO UPDATE SET
                flow_id = excluded.flow_id,
                slot_id = excluded.slot_id,
                slot_no = excluded.slot_no,
                wire_lot_no = excluded.wire_lot_no,
                wire_spec = excluded.wire_spec,
                bind_done = 0,
                mes_disco_done = 0,
                interrupted_at = excluded.interrupted_at;
            """;
        cmd.Parameters.AddWithValue("$flow", FlowId);
        cmd.Parameters.AddWithValue("$sid", slotId);
        cmd.Parameters.AddWithValue("$sno", slotNo.Trim());
        cmd.Parameters.AddWithValue("$lot", wireLotNo.Trim());
        cmd.Parameters.AddWithValue("$spec", string.IsNullOrWhiteSpace(wireSpec) ? DBNull.Value : wireSpec.Trim());
        cmd.ExecuteNonQuery();
    }

    public void UpdateProgress(bool? bindDone = null, bool? mesDiscoDone = null)
    {
        var record = TryGet();
        if (record is null) return;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        var bind = bindDone ?? record.BindDone;
        var mes = mesDiscoDone ?? record.MesDiscoDone;
        cmd.CommandText = """
            UPDATE mh_interrupted_load SET bind_done = $bind, mes_disco_done = $mes WHERE id = 1;
            """;
        cmd.Parameters.AddWithValue("$bind", bind ? 1 : 0);
        cmd.Parameters.AddWithValue("$mes", mes ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public MhInterruptedLoad? TryGet()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT slot_id, slot_no, wire_lot_no, wire_spec, bind_done, mes_disco_done, interrupted_at
            FROM mh_interrupted_load WHERE id = 1;
            """;
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;

        var atText = r.IsDBNull(6) ? null : r.GetString(6);
        DateTime at = DateTime.TryParse(atText, out var parsed) ? parsed : DateTime.UtcNow;

        return new MhInterruptedLoad(
            r.GetInt64(0),
            r.GetString(1),
            r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            r.GetInt64(4) != 0,
            r.GetInt64(5) != 0,
            at);
    }

    public void Clear()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM mh_interrupted_load WHERE id = 1;";
        cmd.ExecuteNonQuery();
    }

    public bool HasLot(string wireLotNo) =>
        TryGet() is { } r && string.Equals(r.WireLotNo, wireLotNo.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void EnsureColumn(Microsoft.Data.Sqlite.SqliteConnection conn, string table, string column, string definition)
    {
        using var check = conn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var r = check.ExecuteReader();
        while (r.Read())
        {
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        try { alter.ExecuteNonQuery(); } catch { /* 已存在 */ }
    }
}
