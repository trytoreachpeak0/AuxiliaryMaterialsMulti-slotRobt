using System.Globalization;

using Oracle.ManagedDataAccess.Client;



namespace WireCabinet.Data;



public enum MesMode { SqliteMock, Oracle }



/// <summary>MES 存储函数在 mock 模式下的可配置返回值。</summary>

public sealed class MesFunctionSettings

{

    /// <summary>Get_Mat_QuotaCheck 返回的配额差值（米）；任意数值表示校验通过。</summary>

    public double QuotaDiff { get; set; } = 0;

    /// <summary>非空时 mock 配额查询失败（如 ORA-20007: 剩余产量不能大于待完工产量！）。</summary>

    public string QuotaError { get; set; } = "";



    /// <summary>FUN_MAT_TRANS_NEW 返回值：空或 SUCCESS 视为成功，其余失败。</summary>

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

            if (!string.IsNullOrWhiteSpace(_settings.QuotaError))

                return DbResult.Failure("[mock] Get_Mat_QuotaCheck", _settings.QuotaError);

            result.RenderedSql = $"[mock] Get_Mat_QuotaCheck => quota_diff = {_settings.QuotaDiff}";

            result.Rows.Add(new(StringComparer.OrdinalIgnoreCase)
            {
                ["quota_diff"] = _settings.QuotaDiff,
                ["result"] = _settings.QuotaDiff
            });

        }

        else // mat_trans.submit_return

        {

            result.RenderedSql = $"[mock] FUN_MAT_TRANS_NEW => submit_result = '{_settings.SubmitResult}'";

            if (!MatTransResult.IsSuccess(_settings.SubmitResult))

                return DbResult.Failure(result.RenderedSql, _settings.SubmitResult);

            result.RowsAffected = 1;

            result.Rows.Add(new(StringComparer.OrdinalIgnoreCase) { [MatTransResult.SubmitResultField] = _settings.SubmitResult });

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

        var rawSql = item.OracleSql ?? item.Sql;

        var isMatTrans = MatTransResult.IsMatTransItem(item);

        var useOutputParam = item.Operation == SqlOperation.Function || isMatTrans;

        // ODP.NET ExecuteReader 不接受纯 SELECT 末尾分号，会报 ORA-00911。

        var sql = useOutputParam ? rawSql : StripTrailingStatementTerminator(rawSql);

        var result = new DbResult { RenderedSql = sql };

        var isWireQuota = item.Id.Contains("wire_quota", StringComparison.OrdinalIgnoreCase);



        if (string.IsNullOrWhiteSpace(_connectionString))

            return DbResult.Failure(sql, "未配置 Oracle 连接串，无法连接真实 MES。请在 appsettings.json 中配置 Mes:ConnectionString。");



        OracleTransaction? tx = null;

        try

        {

            using var conn = new OracleConnection(_connectionString);

            conn.Open();

            if (isMatTrans)

                tx = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();

            cmd.CommandText = sql;

            cmd.BindByName = true;

            if (tx is not null)

                cmd.Transaction = tx;



            foreach (var (key, value) in parameters)

            {

                if (!sql.Contains(":" + key, StringComparison.Ordinal)) continue;

                cmd.Parameters.Add(CreateBindParameter(key, value, isWireQuota));

            }



            if (useOutputParam)

            {

                var outParam = isWireQuota

                    ? new OracleParameter("result", OracleDbType.Decimal) { Direction = System.Data.ParameterDirection.Output }

                    : new OracleParameter("result", OracleDbType.Varchar2, 4000) { Direction = System.Data.ParameterDirection.Output };

                cmd.Parameters.Add(outParam);



                cmd.ExecuteNonQuery();

                if (isWireQuota)

                {

                    var quotaDiff = outParam.Value is null or DBNull ? null : outParam.Value.ToString();

                    result.Rows.Add(new(StringComparer.OrdinalIgnoreCase)

                    {

                        ["quota_diff"] = quotaDiff,

                        ["result"] = quotaDiff

                    });

                    result.RowsAffected = 1;

                }

                else

                {

                    var submitResult = outParam.Value?.ToString();

                    if (isMatTrans)

                    {

                        if (MatTransResult.IsSuccess(submitResult))

                        {

                            tx!.Commit();

                            result.RowsAffected = 1;

                            result.Rows.Add(new(StringComparer.OrdinalIgnoreCase) { [MatTransResult.SubmitResultField] = submitResult });

                        }

                        else

                        {

                            tx!.Rollback();

                            result.Error = string.IsNullOrWhiteSpace(submitResult) ? "MES 返回失败" : submitResult;

                        }

                    }

                    else

                    {

                        result.Rows.Add(new(StringComparer.OrdinalIgnoreCase) { [MatTransResult.SubmitResultField] = submitResult });

                        result.RowsAffected = 1;

                    }

                }

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

            try { tx?.Rollback(); } catch { /* ignore rollback failure */ }

            result.Error = ex.Message;

        }

        return result;

    }



    private static OracleParameter CreateBindParameter(string key, object? value, bool isWireQuota)

    {

        if (isWireQuota && string.Equals(key, "V_thzl", StringComparison.OrdinalIgnoreCase))

        {

            var s = value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

            return new OracleParameter(key, OracleDbType.Varchar2) { Value = s ?? (object)DBNull.Value };

        }



        if (isWireQuota && string.Equals(key, "V_sycl", StringComparison.OrdinalIgnoreCase))

        {

            var n = ToNumber(value);

            return new OracleParameter(key, OracleDbType.Decimal) { Value = n ?? (object)DBNull.Value };

        }



        return new OracleParameter(key, value ?? DBNull.Value);

    }



    private static decimal? ToNumber(object? value)

    {

        if (value is null or DBNull) return null;

        if (value is decimal d) return d;

        if (value is int i) return i;

        if (value is long l) return l;

        if (value is double db) return (decimal)db;

        return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number,

            CultureInfo.InvariantCulture, out var n)

            ? n

            : null;

    }



    private static string StripTrailingStatementTerminator(string sql)

    {

        var s = sql.TrimEnd();

        while (s.EndsWith(';'))

            s = s[..^1].TrimEnd();

        return s;

    }

}

