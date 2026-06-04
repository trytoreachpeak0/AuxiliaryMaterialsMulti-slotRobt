using Oracle.ManagedDataAccess.Client;

namespace WireCabinet.Data;

public enum MesMode { SqliteMock, Oracle }

/// <summary>MES 存储函数在 mock 模式下的可配置返回值。</summary>
public sealed class MesFunctionSettings
{
    /// <summary>Get_Mat_QuotaCheck 返回的配额差值。&lt;=500 允许归还。</summary>
    public double QuotaDiff { get; set; } = 0;

    /// <summary>FUN_MAT_TRANS_NEW 返回值：SUCCESS / 空 / 含 ORA-01403 视为成功，其余失败。</summary>
    public string SubmitResult { get; set; } = "SUCCESS";
}

/// <summary>MES 访问接口，可在 SQLite mock 与 Oracle 之间切换。</summary>
public interface IMesGateway
{
    MesMode Mode { get; }
    DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters);
}

/// <summary>SQLite mock：MES 只读查询跑 mock 表的 SQLite SQL，存储函数用可配置结果。</summary>
public sealed class SqliteMockMesGateway : IMesGateway
{
    private readonly SqliteDb _db;
    private readonly MesFunctionSettings _settings;

    public SqliteMockMesGateway(SqliteDb db, MesFunctionSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    public MesMode Mode => MesMode.SqliteMock;

    public DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters)
    {
        if (item.Operation == SqlOperation.Function)
            return RunFunctionMock(item);

        return _db.Query(item.Sql, parameters);
    }

    private DbResult RunFunctionMock(SqlCatalogItem item)
    {
        var result = new DbResult();
        if (item.Id.Contains("wire_quota", StringComparison.OrdinalIgnoreCase))
        {
            result.RenderedSql = $"[mock] Get_Mat_QuotaCheck => quota_diff = {_settings.QuotaDiff}";
            result.Rows.Add(new(StringComparer.OrdinalIgnoreCase) { ["quota_diff"] = _settings.QuotaDiff });
        }
        else // mat_trans.submit_return
        {
            result.RenderedSql = $"[mock] FUN_MAT_TRANS_NEW => submit_result = '{_settings.SubmitResult}'";
            result.RowsAffected = 1;
            result.Rows.Add(new(StringComparer.OrdinalIgnoreCase) { ["submit_result"] = _settings.SubmitResult });
        }
        return result;
    }
}

/// <summary>Oracle：跑 mes_mock 条目中保留的原生 Oracle SQL / PL/SQL。需要连接串。</summary>
public sealed class OracleMesGateway : IMesGateway
{
    private readonly string _connectionString;

    public OracleMesGateway(string connectionString) => _connectionString = connectionString;

    public MesMode Mode => MesMode.Oracle;

    public DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters)
    {
        var sql = item.OracleSql ?? item.Sql;
        var result = new DbResult { RenderedSql = sql };

        if (string.IsNullOrWhiteSpace(_connectionString))
            return DbResult.Failure(sql, "未配置 Oracle 连接串，无法连接真实 MES。请在 appsettings.json 中配置 Mes:ConnectionString。");

        try
        {
            using var conn = new OracleConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.BindByName = true;

            foreach (var (key, value) in parameters)
            {
                if (!sql.Contains(":" + key, StringComparison.Ordinal)) continue;
                cmd.Parameters.Add(new OracleParameter(key, value ?? DBNull.Value));
            }

            if (item.Operation == SqlOperation.Function)
            {
                var outParam = new OracleParameter("result", OracleDbType.Varchar2, 4000) { Direction = System.Data.ParameterDirection.Output };
                cmd.Parameters.Add(outParam);
                cmd.ExecuteNonQuery();
                var field = item.Id.Contains("wire_quota", StringComparison.OrdinalIgnoreCase) ? "quota_diff" : "submit_result";
                result.Rows.Add(new(StringComparer.OrdinalIgnoreCase) { [field] = outParam.Value?.ToString() });
                result.RowsAffected = 1;
            }
            else
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    result.Rows.Add(row);
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }
}
