namespace WireCabinet.Data;

public sealed class WireMesDiscoSyncStore
{
    private readonly SqliteDb _db;

    public WireMesDiscoSyncStore(SqliteDb db) => _db = db;

    public void EnsureSchema() => WireMesDiscoSchema.EnsurePendingTable(_db);

    public void UpsertPending(string wireLotNo, WireMesDiscoAction action, WireMesDiscoFailureKind kind, string? error)
    {
        var lot = wireLotNo.Trim();
        var act = action == WireMesDiscoAction.Set ? "set" : "clear";
        var fk = kind == WireMesDiscoFailureKind.Inline ? "inline" : "reconcile";
        var nextRetry = DateTime.UtcNow.AddSeconds(30).ToString("o");

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO wire_mes_disco_sync (wire_lot_no, action, status, failure_kind, last_error, retry_count, next_retry_at, updated_at)
            VALUES ($lot, $act, 'pending', $fk, $err, 0, $next, datetime('now'))
            ON CONFLICT(wire_lot_no, action) DO UPDATE SET
                status = 'pending',
                failure_kind = excluded.failure_kind,
                last_error = excluded.last_error,
                next_retry_at = excluded.next_retry_at,
                updated_at = datetime('now')
            WHERE wire_mes_disco_sync.status IN ('pending', 'failed');
            """;
        cmd.Parameters.AddWithValue("$lot", lot);
        cmd.Parameters.AddWithValue("$act", act);
        cmd.Parameters.AddWithValue("$fk", fk);
        cmd.Parameters.AddWithValue("$err", (object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$next", nextRetry);
        cmd.ExecuteNonQuery();
    }

    public void MarkSuccess(string wireLotNo, WireMesDiscoAction action)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE wire_mes_disco_sync
            SET status = 'success', last_error = NULL, updated_at = datetime('now')
            WHERE wire_lot_no = $lot AND action = $act;
            """;
        cmd.Parameters.AddWithValue("$lot", wireLotNo.Trim());
        cmd.Parameters.AddWithValue("$act", action == WireMesDiscoAction.Set ? "set" : "clear");
        cmd.ExecuteNonQuery();
    }

    public void MarkFailed(string wireLotNo, WireMesDiscoAction action, string error, int retryCount, DateTime? nextRetry)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE wire_mes_disco_sync
            SET status = 'failed', last_error = $err, retry_count = $rc,
                next_retry_at = $next, updated_at = datetime('now')
            WHERE wire_lot_no = $lot AND action = $act;
            """;
        cmd.Parameters.AddWithValue("$lot", wireLotNo.Trim());
        cmd.Parameters.AddWithValue("$act", action == WireMesDiscoAction.Set ? "set" : "clear");
        cmd.Parameters.AddWithValue("$err", error);
        cmd.Parameters.AddWithValue("$rc", retryCount);
        cmd.Parameters.AddWithValue("$next", nextRetry.HasValue ? nextRetry.Value.ToString("o") : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void ScheduleRetry(long id, int retryCount, DateTime nextRetry, string? error)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE wire_mes_disco_sync
            SET status = 'pending', retry_count = $rc, next_retry_at = $next,
                last_error = $err, updated_at = datetime('now')
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$rc", retryCount);
        cmd.Parameters.AddWithValue("$next", nextRetry.ToString("o"));
        cmd.Parameters.AddWithValue("$err", (object?)error ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void MarkManualResolved(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE wire_mes_disco_sync SET status = 'manual_resolved', updated_at = datetime('now') WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<WireMesDiscoPendingRow> ListActive()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, wire_lot_no, action, status, failure_kind, last_error, retry_count, next_retry_at, updated_at
            FROM wire_mes_disco_sync
            WHERE status IN ('pending', 'failed')
            ORDER BY updated_at DESC;
            """;
        return ReadRows(cmd);
    }

    public IReadOnlyList<WireMesDiscoPendingRow> ListDueForRetry(DateTime nowUtc)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, wire_lot_no, action, status, failure_kind, last_error, retry_count, next_retry_at, updated_at
            FROM wire_mes_disco_sync
            WHERE status = 'pending'
              AND (next_retry_at IS NULL OR next_retry_at <= $now)
            ORDER BY next_retry_at ASC;
            """;
        cmd.Parameters.AddWithValue("$now", nowUtc.ToString("o"));
        return ReadRows(cmd);
    }

    public WireMesDiscoPendingRow? TryGet(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, wire_lot_no, action, status, failure_kind, last_error, retry_count, next_retry_at, updated_at
            FROM wire_mes_disco_sync WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        return ReadRows(cmd).FirstOrDefault();
    }

    private static List<WireMesDiscoPendingRow> ReadRows(Microsoft.Data.Sqlite.SqliteCommand cmd)
    {
        var list = new List<WireMesDiscoPendingRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new WireMesDiscoPendingRow(
                r.GetInt64(0),
                r.GetString(1),
                ParseAction(r.GetString(2)),
                ParseStatus(r.GetString(3)),
                ParseKind(r.GetString(4)),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetInt32(6),
                ParseOptionalDate(r.IsDBNull(7) ? null : r.GetString(7)),
                ParseDate(r.GetString(8))));
        }
        return list;
    }

    private static WireMesDiscoAction ParseAction(string s) =>
        string.Equals(s, "clear", StringComparison.OrdinalIgnoreCase) ? WireMesDiscoAction.Clear : WireMesDiscoAction.Set;

    private static WireMesDiscoSyncStatus ParseStatus(string s) => s.ToLowerInvariant() switch
    {
        "success" => WireMesDiscoSyncStatus.Success,
        "failed" => WireMesDiscoSyncStatus.Failed,
        "manual_resolved" => WireMesDiscoSyncStatus.ManualResolved,
        _ => WireMesDiscoSyncStatus.Pending
    };

    private static WireMesDiscoFailureKind ParseKind(string s) =>
        string.Equals(s, "reconcile", StringComparison.OrdinalIgnoreCase) ? WireMesDiscoFailureKind.Reconcile : WireMesDiscoFailureKind.Inline;

    private static DateTime ParseDate(string text) =>
        DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow;

    private static DateTime? ParseOptionalDate(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : ParseDate(text);
}
