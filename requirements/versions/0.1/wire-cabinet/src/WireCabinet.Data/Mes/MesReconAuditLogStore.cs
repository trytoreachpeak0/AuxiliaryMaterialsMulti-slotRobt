namespace WireCabinet.Data;

public sealed class MesReconAuditLogStore
{
    private readonly SqliteDb _db;

    public MesReconAuditLogStore(SqliteDb db) => _db = db;

    public void EnsureSchema() => WireMesDiscoSchema.EnsureAuditTable(_db);

    public void Append(string action, string? wireLotNo, string detail)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mes_recon_audit_log (action, wire_lot_no, detail) VALUES ($act, $lot, $detail);
            """;
        cmd.Parameters.AddWithValue("$act", action);
        cmd.Parameters.AddWithValue("$lot", string.IsNullOrWhiteSpace(wireLotNo) ? DBNull.Value : wireLotNo.Trim());
        cmd.Parameters.AddWithValue("$detail", detail);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<MesReconAuditEntry> ListRecent(int limit = 50)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, action, wire_lot_no, detail, created_at
            FROM mes_recon_audit_log ORDER BY id DESC LIMIT $lim;
            """;
        cmd.Parameters.AddWithValue("$lim", limit);
        var list = new List<MesReconAuditEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var at = DateTime.TryParse(r.GetString(4), out var dt) ? dt : DateTime.UtcNow;
            list.Add(new MesReconAuditEntry(
                r.GetInt64(0),
                r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3),
                at));
        }
        return list;
    }
}
