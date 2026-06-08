using System.Text;

namespace WireCabinet.Data;

/// <summary>Trace 参数展示：Oracle 绑定类型 + IN/OUT 方向 + 值。</summary>
public static class SqlBindTrace
{
    public static (string Compact, string Detail) Format(
        SqlCatalogItem? item,
        bool isOracle,
        IDictionary<string, object?>? prms,
        Dictionary<string, object?>? outRow = null)
    {
        if (prms is null || prms.Count == 0)
            return ("", "");

        var names = GetOrderedParamNames(item, prms, outRow);
        if (names.Count == 0)
            return ("", "");

        var compactParts = new List<string>();
        var detailLines = new List<string>();

        foreach (var name in names)
        {
            var isOut = IsOutParam(item, name);
            object? val = isOut
                ? GetOutValue(name, outRow)
                : prms.TryGetValue(name, out var v) ? v : null;

            var typeDir = isOracle
                ? OracleBindResolver.FormatTypeDirection(item, name)
                : FormatSqliteTypeDirection(val, isOut);

            compactParts.Add($"{name}:{typeDir}={DisplayValue(val, isOut)}");
            detailLines.Add(FormatDetailLine(name, typeDir, val, isOut));
        }

        return (string.Join(", ", compactParts), string.Join(Environment.NewLine, detailLines));
    }

    public static string FormatCompact(
        SqlCatalogItem? item,
        bool isOracle,
        IDictionary<string, object?>? prms,
        Dictionary<string, object?>? outRow = null) =>
        Format(item, isOracle, prms, outRow).Compact;

    private static List<string> GetOrderedParamNames(
        SqlCatalogItem? item,
        IDictionary<string, object?> prms,
        Dictionary<string, object?>? outRow)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (item?.Params.Count > 0)
        {
            foreach (var spec in item.Params)
            {
                if (!seen.Add(spec.Name)) continue;
                if (spec.Direction == SqlParamDirection.Out || prms.ContainsKey(spec.Name))
                    names.Add(spec.Name);
            }
        }

        foreach (var key in prms.Keys)
        {
            if (seen.Add(key))
                names.Add(key);
        }

        if (item is not null
            && HasOutResultParam(item)
            && seen.Add("result"))
        {
            names.Add("result");
        }

        return names;
    }

    private static bool HasOutResultParam(SqlCatalogItem item) =>
        item.Params.Any(p =>
            p.Direction == SqlParamDirection.Out
            && string.Equals(p.Name, "result", StringComparison.OrdinalIgnoreCase))
        || item.Operation == SqlOperation.Function
        || MatTransResult.IsMatTransItem(item);

    private static bool IsOutParam(SqlCatalogItem? item, string name)
    {
        if (!string.Equals(name, "result", StringComparison.OrdinalIgnoreCase))
            return false;

        var spec = item?.Params.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (spec is not null)
            return spec.Direction == SqlParamDirection.Out;

        if (item is null)
            return false;

        return item.Operation == SqlOperation.Function
               || MatTransResult.IsMatTransItem(item)
               || item.Id.Contains("wire_quota", StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetOutValue(string name, Dictionary<string, object?>? outRow)
    {
        if (outRow is null) return null;
        if (outRow.TryGetValue(name, out var v)) return v;
        if (outRow.TryGetValue(MatTransResult.SubmitResultField, out v)) return v;
        if (outRow.TryGetValue("quota_diff", out v)) return v;
        return null;
    }

    private static string FormatSqliteTypeDirection(object? value, bool isOut)
    {
        if (isOut) return "OUT";
        return InferSqliteType(value) + "(IN)";
    }

    private static string InferSqliteType(object? value) => value switch
    {
        null or DBNull => "TEXT",
        int or long => "INTEGER",
        float or double or decimal => "REAL",
        _ => "TEXT"
    };

    private static string DisplayValue(object? val, bool isOut)
    {
        if (isOut && (val is null or DBNull))
            return "(OUT)";
        if (val is null or DBNull)
            return "NULL";
        if (val is string s && s.Length == 0)
            return "\"\"";
        return val.ToString() ?? "\"\"";
    }

    private static string FormatDetailLine(string name, string typeDir, object? val, bool isOut)
    {
        var dir = typeDir.Contains("(OUT)", StringComparison.OrdinalIgnoreCase) ? "OUT" : "IN";
        var type = typeDir.Replace("(IN)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(OUT)", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return $"{name,-16} {type,-10} {dir,-4} = {DisplayValue(val, isOut)}";
    }
}
