using System.Globalization;

namespace WireCabinet.Data;

/// <summary>MES / Oracle 日期时间字符串：固定为 yyyy-MM-dd HH:mm:ss，不依赖系统区域设置。</summary>
public static class MesDateTimeFormat
{
    public const string OraclePattern = "yyyy-MM-dd HH:mm:ss";

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static readonly string[] ParsePatterns =
    [
        OraclePattern,
        "yyyy-MM-dd",
        "M/d/yyyy h:mm:ss tt",
        "M/d/yyyy h:mm tt",
        "M/d/yyyy",
        "MM/dd/yyyy h:mm:ss tt",
        "MM/dd/yyyy h:mm tt",
        "MM/dd/yyyy",
        "dd-MMM-yy",
        "dd-MMM-yyyy",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd"
    ];

    /// <summary>将 DateTime 或各类日期字符串转为 Oracle TO_DATE 可识别的格式；空值返回 null 或空字符串。</summary>
    public static string? ToOracleString(object? value)
    {
        if (value is null or DBNull) return null;

        if (value is DateTime dt)
            return dt.ToString(OraclePattern, CultureInfo.InvariantCulture);

        if (value is DateTimeOffset dto)
            return dto.DateTime.ToString(OraclePattern, CultureInfo.InvariantCulture);

        var s = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(s)) return "";

        if (DateTime.TryParseExact(s, OraclePattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return s;

        if (TryParseDateTime(s, out var parsed))
            return parsed.ToString(OraclePattern, CultureInfo.InvariantCulture);

        return s;
    }

    private static bool TryParseDateTime(string s, out DateTime result)
    {
        if (DateTime.TryParseExact(s, ParsePatterns, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out result))
            return true;

        if (DateTime.TryParse(s, EnUs, DateTimeStyles.AllowWhiteSpaces, out result))
            return true;

        return false;
    }
}
