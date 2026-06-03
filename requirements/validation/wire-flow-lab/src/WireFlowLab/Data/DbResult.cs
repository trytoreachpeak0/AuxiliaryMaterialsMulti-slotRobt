namespace WireFlowLab.Data;

/// <summary>SQL 执行结果（查询行集 / 写入影响行数 / 错误）。</summary>
public sealed class DbResult
{
    public List<Dictionary<string, object?>> Rows { get; } = new();
    public int RowsAffected { get; set; }
    public string RenderedSql { get; set; } = "";
    public string? Error { get; set; }

    public bool HasError => Error is not null;
    public int Count => Rows.Count;
    public Dictionary<string, object?>? First => Rows.Count > 0 ? Rows[0] : null;

    public static DbResult Failure(string sql, string error) => new() { RenderedSql = sql, Error = error };
}
