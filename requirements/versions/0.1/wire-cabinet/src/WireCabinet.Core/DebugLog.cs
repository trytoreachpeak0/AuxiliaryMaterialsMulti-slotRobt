using System.Text.Json;

namespace WireCabinet.Core;

/// <summary>调试会话 NDJSON 日志（session 6d4bf9）。</summary>
public static class DebugLog
{
    private static string LogPath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (string.Equals(dir.Name, "AuxiliaryMaterialsMulti-slotRobt", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(dir.FullName, "debug-6d4bf9.log");
                dir = dir.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "debug-6d4bf9.log");
        }
    }

    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        // #region agent log
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                sessionId = "6d4bf9",
                hypothesisId,
                location,
                message,
                data,
                runId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* ignore */ }
        // #endregion
    }
}
