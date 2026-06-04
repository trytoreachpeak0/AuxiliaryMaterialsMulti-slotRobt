using System.Text.RegularExpressions;

namespace WireCabinet.Hmi.Services;

/// <summary>将 slot-io-mapping 中的 door_position 格式化为维护界面可读文案。</summary>
public static partial class SlotPositionFormatter
{
    [GeneratedRegex(@"^(front|rear)\((\d+),(\d+)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex DoorPositionPattern();

    public static string Format(string? doorPosition)
    {
        if (string.IsNullOrWhiteSpace(doorPosition))
            return "—";

        var m = DoorPositionPattern().Match(doorPosition.Trim());
        if (!m.Success)
            return doorPosition;

        var side = m.Groups[1].Value.Equals("front", StringComparison.OrdinalIgnoreCase) ? "前柜" : "后柜";
        return $"{side} · 第{m.Groups[2].Value}列 · 第{m.Groups[3].Value}行";
    }
}
