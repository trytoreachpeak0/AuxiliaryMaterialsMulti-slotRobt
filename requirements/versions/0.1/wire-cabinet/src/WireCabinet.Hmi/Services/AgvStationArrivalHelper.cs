using WireCabinet.Rcs;

namespace WireCabinet.Hmi.Services;

/// <summary>与自动切屏一致的「已到站」判定，供 UI 门控与导航复用。</summary>
public static class AgvStationArrivalHelper
{
    public static bool IsAgvArrivedForStationUi(AgvServices agv)
    {
        if (agv.Move?.State == MoveUiState.Arrived)
            return true;

        var move = agv.LastMoveState ?? "";
        var sys = agv.LastSysState ?? "";
        if (!string.Equals(sys, "IDLE", StringComparison.OrdinalIgnoreCase))
            return false;

        return move.Contains("FINISHED", StringComparison.OrdinalIgnoreCase)
               || move.Contains("AT_NA", StringComparison.OrdinalIgnoreCase)
               || move.Equals("AT_NA", StringComparison.OrdinalIgnoreCase);
    }
}
