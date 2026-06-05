namespace WireCabinet.Rcs;

using AgvDispatch.Sdk;

/// <summary>将 RCS locationState 映射为 HMI 中文。</summary>
public static class LocationStateDisplay
{
    public static string Format(string? locationState, AgvStateThresholds thresholds)
    {
        if (string.IsNullOrWhiteSpace(locationState))
            return "未知";

        if (thresholds.SuccessLocationStates.Contains(locationState, StringComparer.OrdinalIgnoreCase))
            return "定位成功";

        if (thresholds.RunningLocationStates.Contains(locationState, StringComparer.OrdinalIgnoreCase))
            return "定位中";

        if (thresholds.FailedLocationStateContains.Any(f =>
                locationState.Contains(f, StringComparison.OrdinalIgnoreCase)))
            return "定位失败";

        return locationState;
    }
}
