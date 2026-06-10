namespace WireCabinet.Data;

public sealed record OpInterruptedIssue(
    long SlotId,
    string SlotNo,
    string WireLotNo,
    bool SubmitIssueDone,
    bool PickupDone,
    bool MesDiscoDone,
    DateTime InterruptedAt);

public sealed class OpInterruptedIssueStore
{
    private const int RowId = 1;

    private readonly SqliteDb _db;

    public OpInterruptedIssueStore(SqliteDb db) => _db = db;

    public void EnsureSchema() => WireMesDiscoSchema.EnsureOpInterruptedIssueTable(_db);

    public void Save(long slotId, string slotNo, string wireLotNo, bool submitIssueDone)
    {
        if (slotId <= 0 || string.IsNullOrWhiteSpace(slotNo) || string.IsNullOrWhiteSpace(wireLotNo))
            return;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO op_interrupted_issue (id, slot_id, slot_no, wire_lot_no, submit_issue_done, pickup_done, mes_disco_done, interrupted_at)
            VALUES (1, $sid, $sno, $lot, $sub, 0, 0, datetime('now'))
            ON CONFLICT(id) DO UPDATE SET
                slot_id = excluded.slot_id,
                slot_no = excluded.slot_no,
                wire_lot_no = excluded.wire_lot_no,
                submit_issue_done = excluded.submit_issue_done,
                pickup_done = excluded.pickup_done,
                mes_disco_done = excluded.mes_disco_done,
                interrupted_at = excluded.interrupted_at;
            """;
        cmd.Parameters.AddWithValue("$sid", slotId);
        cmd.Parameters.AddWithValue("$sno", slotNo.Trim());
        cmd.Parameters.AddWithValue("$lot", wireLotNo.Trim());
        cmd.Parameters.AddWithValue("$sub", submitIssueDone ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void UpdateProgress(bool? pickupDone = null, bool? mesDiscoDone = null)
    {
        var record = TryGet();
        if (record is null) return;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        var pickup = pickupDone ?? record.PickupDone;
        var mes = mesDiscoDone ?? record.MesDiscoDone;
        cmd.CommandText = """
            UPDATE op_interrupted_issue
            SET pickup_done = $pickup, mes_disco_done = $mes
            WHERE id = 1;
            """;
        cmd.Parameters.AddWithValue("$pickup", pickup ? 1 : 0);
        cmd.Parameters.AddWithValue("$mes", mes ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public OpInterruptedIssue? TryGet()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT slot_id, slot_no, wire_lot_no, submit_issue_done, pickup_done, mes_disco_done, interrupted_at
            FROM op_interrupted_issue WHERE id = 1;
            """;
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var at = DateTime.TryParse(r.IsDBNull(6) ? null : r.GetString(6), out var dt) ? dt : DateTime.UtcNow;
        return new OpInterruptedIssue(
            r.GetInt64(0),
            r.GetString(1),
            r.GetString(2),
            r.GetInt64(3) != 0,
            r.GetInt64(4) != 0,
            r.GetInt64(5) != 0,
            at);
    }

    public void Clear()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM op_interrupted_issue WHERE id = 1;";
        cmd.ExecuteNonQuery();
    }

    public bool HasLot(string wireLotNo) =>
        TryGet() is { } r && string.Equals(r.WireLotNo, wireLotNo.Trim(), StringComparison.OrdinalIgnoreCase);
}
