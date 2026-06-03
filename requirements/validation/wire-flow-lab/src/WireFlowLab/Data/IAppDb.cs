namespace WireFlowLab.Data;

/// <summary>应用数据库访问接口（真实 SQLite 执行）。</summary>
public interface IAppDb
{
    DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters);
    DbResult RunRaw(string sql, IDictionary<string, object?> parameters, bool isWrite);
}

public sealed class SqliteAppDb : IAppDb
{
    private readonly SqliteDb _db;

    public SqliteAppDb(SqliteDb db) => _db = db;

    public DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters)
        => item.Operation == SqlOperation.Write
            ? _db.Execute(item.Sql, parameters)
            : _db.Query(item.Sql, parameters);

    public DbResult RunRaw(string sql, IDictionary<string, object?> parameters, bool isWrite)
        => isWrite ? _db.Execute(sql, parameters) : _db.Query(sql, parameters);
}
