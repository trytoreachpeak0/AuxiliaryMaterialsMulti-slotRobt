using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace WireCabinet.Data;

/// <summary>Oracle 绑定类型解析，与 OracleMesGateway 实际绑定规则一致。</summary>
public static class OracleBindResolver
{
    public sealed record BindSpec(string TypeName, ParameterDirection Direction);

    public static BindSpec ResolveSpec(SqlCatalogItem? item, string paramName)
    {
        var isWireQuota = item?.Id.Contains("wire_quota", StringComparison.OrdinalIgnoreCase) == true;
        var isMatTrans = item is not null && MatTransResult.IsMatTransItem(item);

        if (string.Equals(paramName, "result", StringComparison.OrdinalIgnoreCase))
        {
            if (isWireQuota)
                return new BindSpec("Decimal", ParameterDirection.Output);
            if (isMatTrans || item?.Operation == SqlOperation.Function)
                return new BindSpec("Varchar2", ParameterDirection.Output);
        }

        if (isWireQuota && string.Equals(paramName, "V_thzl", StringComparison.OrdinalIgnoreCase))
            return new BindSpec("Varchar2", ParameterDirection.Input);
        if (isWireQuota && string.Equals(paramName, "V_sycl", StringComparison.OrdinalIgnoreCase))
            return new BindSpec("Decimal", ParameterDirection.Input);

        var logicalType = item?.Params
            .FirstOrDefault(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase))
            ?.LogicalType ?? "string";

        if (string.Equals(logicalType, "number", StringComparison.OrdinalIgnoreCase))
            return new BindSpec("Decimal", ParameterDirection.Input);

        return new BindSpec("Varchar2", ParameterDirection.Input);
    }

    public static OracleParameter CreateInParameter(SqlCatalogItem item, string key, object? value)
    {
        var spec = ResolveSpec(item, key);
        if (spec.Direction != ParameterDirection.Input)
            throw new InvalidOperationException($"参数 {key} 不是 IN 绑定。");

        if (string.Equals(spec.TypeName, "Decimal", StringComparison.OrdinalIgnoreCase))
        {
            var n = ToNumber(value);
            return new OracleParameter(key, OracleDbType.Decimal) { Value = n ?? (object)DBNull.Value };
        }

        if (string.Equals(spec.TypeName, "Varchar2", StringComparison.OrdinalIgnoreCase))
        {
            var s = value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            return new OracleParameter(key, OracleDbType.Varchar2) { Value = s ?? (object)DBNull.Value };
        }

        return new OracleParameter(key, value ?? DBNull.Value);
    }

    public static OracleParameter CreateOutParameter(SqlCatalogItem item)
    {
        var spec = ResolveSpec(item, "result");
        return string.Equals(spec.TypeName, "Decimal", StringComparison.OrdinalIgnoreCase)
            ? new OracleParameter("result", OracleDbType.Decimal) { Direction = ParameterDirection.Output }
            : new OracleParameter("result", OracleDbType.Varchar2, 4000) { Direction = ParameterDirection.Output };
    }

    public static string FormatTypeDirection(SqlCatalogItem? item, string paramName)
    {
        var spec = ResolveSpec(item, paramName);
        var dir = spec.Direction == ParameterDirection.Output ? "OUT" : "IN";
        return $"{spec.TypeName}({dir})";
    }

    internal static decimal? ToNumber(object? value)
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
}
