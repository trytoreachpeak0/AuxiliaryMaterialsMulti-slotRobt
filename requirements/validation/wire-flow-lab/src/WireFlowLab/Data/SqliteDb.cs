using System.IO;
using Microsoft.Data.Sqlite;
using WireFlowLab.Infrastructure;

namespace WireFlowLab.Data;

/// <summary>
/// SQLite 连接与执行封装。应用库与 MES mock 共用同一个数据库文件。
/// 首次创建时执行 schema + seed。
/// </summary>
public sealed class SqliteDb
{
    private readonly string _connectionString;

    public string DbPath { get; }

    public SqliteDb(string dbPath)
    {
        DbPath = dbPath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
    }

    /// <summary>初始化数据库：建库 + 灌种子。recreate=true 时删除旧文件重建。</summary>
    public void Initialize(bool recreate = false)
    {
        if (recreate && File.Exists(DbPath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(DbPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

        var schema = File.ReadAllText(LabPaths.SchemaScript);
        var seed = File.ReadAllText(LabPaths.SeedScript);

        using var conn = Open();
        ExecScript(conn, schema);
        ExecScript(conn, seed);
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static void ExecScript(SqliteConnection conn, string script)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = script;
        cmd.ExecuteNonQuery();
    }

    public DbResult Query(string sql, IDictionary<string, object?> parameters)
    {
        var result = new DbResult { RenderedSql = RenderForDisplay(sql, parameters) };
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            Bind(cmd, sql, parameters);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = val;
                }
                result.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    public DbResult Execute(string sql, IDictionary<string, object?> parameters)
    {
        var result = new DbResult { RenderedSql = RenderForDisplay(sql, parameters) };
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            Bind(cmd, sql, parameters);
            result.RowsAffected = cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    private static void Bind(SqliteCommand cmd, string sql, IDictionary<string, object?> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            // 仅绑定 SQL 中实际出现的参数，避免多余参数报错
            if (!sql.Contains(":" + key, StringComparison.Ordinal)) continue;
            cmd.Parameters.AddWithValue(":" + key, value ?? DBNull.Value);
        }
    }

    private static string RenderForDisplay(string sql, IDictionary<string, object?> parameters)
    {
        var rendered = sql;
        foreach (var (key, value) in parameters.OrderByDescending(p => p.Key.Length))
        {
            var display = value is null ? "NULL" : $"'{value}'";
            rendered = rendered.Replace(":" + key, display, StringComparison.Ordinal);
        }
        return rendered.Trim();
    }
}
