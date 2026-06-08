using System.Text.Json;
using WireCabinet.Core;

namespace WireCabinet.Data;

/// <summary>Debug session NDJSON logger (agent instrumentation).</summary>
internal static class DebugAgentLog
{
    private static readonly string LogPath = Path.Combine(CabinetPaths.RepoRoot, "debug-07b53e.log");

    public static void Write(string hypothesisId, string location, string message, object? data = null)
    {
        // #region agent log
        try
        {
            var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["sessionId"] = "07b53e",
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data
            });
            File.AppendAllText(LogPath, payload + Environment.NewLine);
        }
        catch { /* ignore */ }
        // #endregion
    }
}
