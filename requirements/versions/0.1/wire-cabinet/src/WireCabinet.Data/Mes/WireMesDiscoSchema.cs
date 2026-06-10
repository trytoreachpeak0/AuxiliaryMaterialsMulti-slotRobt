namespace WireCabinet.Data;

/// <summary>DISCOEQPNO 同步相关 SQLite 表与 mock MES 列迁移。</summary>
public static class WireMesDiscoSchema
{
    public static void EnsureAll(SqliteDb db)
    {
        EnsurePendingTable(db);
        EnsureAuditTable(db);
        EnsureOpInterruptedIssueTable(db);
        EnsureMockMesColumn(db);
    }

    public static void EnsurePendingTable(SqliteDb db)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS wire_mes_disco_sync (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                wire_lot_no     TEXT NOT NULL,
                action          TEXT NOT NULL CHECK (action IN ('set', 'clear')),
                status          TEXT NOT NULL CHECK (status IN ('pending', 'success', 'failed', 'manual_resolved')),
                failure_kind    TEXT NOT NULL CHECK (failure_kind IN ('inline', 'reconcile')),
                last_error      TEXT,
                retry_count     INTEGER NOT NULL DEFAULT 0,
                next_retry_at   TEXT,
                updated_at      TEXT NOT NULL DEFAULT (datetime('now')),
                UNIQUE(wire_lot_no, action)
            );
            CREATE INDEX IF NOT EXISTS idx_wire_mes_disco_sync_status
                ON wire_mes_disco_sync(status, next_retry_at);
            """;
        cmd.ExecuteNonQuery();
    }

    public static void EnsureAuditTable(SqliteDb db)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS mes_recon_audit_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                action      TEXT NOT NULL,
                wire_lot_no TEXT,
                detail      TEXT NOT NULL,
                created_at  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public static void EnsureOpInterruptedIssueTable(SqliteDb db)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS op_interrupted_issue (
                id                  INTEGER PRIMARY KEY CHECK (id = 1),
                slot_id             INTEGER NOT NULL,
                slot_no             TEXT NOT NULL,
                wire_lot_no         TEXT NOT NULL,
                submit_issue_done   INTEGER NOT NULL DEFAULT 0,
                pickup_done         INTEGER NOT NULL DEFAULT 0,
                mes_disco_done      INTEGER NOT NULL DEFAULT 0,
                interrupted_at      TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>mock MES 表增加 DISCOEQPNO 列（若表不存在则创建最小结构）。</summary>
    public static void EnsureMockMesColumn(SqliteDb db)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS v_fw_material_indetail (
                sublot      TEXT,
                spec        TEXT,
                type        TEXT,
                code        TEXT,
                shelflife   TEXT,
                matlot      TEXT,
                qty         REAL,
                state       TEXT,
                DISCOEQPNO  TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        if (!ColumnExists(conn, "v_fw_material_indetail", "DISCOEQPNO"))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE v_fw_material_indetail ADD COLUMN DISCOEQPNO TEXT;";
            try { alter.ExecuteNonQuery(); } catch { /* 列已存在 */ }
        }
    }

    private static bool ColumnExists(Microsoft.Data.Sqlite.SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
