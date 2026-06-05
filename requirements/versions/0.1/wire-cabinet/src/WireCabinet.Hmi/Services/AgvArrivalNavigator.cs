using WireCabinet.Rcs;

namespace WireCabinet.Hmi.Services;

/// <summary>车辆到达作业站后自动切换 OP/MH 界面（防抖）。</summary>
public sealed class AgvArrivalNavigator
{
    private int? _lastNavigatedPosition;

    public void ResetIfLeftStation(int currentPosition)
    {
        if (_lastNavigatedPosition is int last && currentPosition != last)
            _lastNavigatedPosition = null;
    }

    public WorkStationConfig? TryGetArrivedStation(AgvServices agv)
    {
        if (agv.LastPosition <= 0)
            return null;

        var pos = agv.LastPosition;
        if (_lastNavigatedPosition == pos)
            return null;

        var station = agv.Stations.EnabledWorkStations
            .FirstOrDefault(s => s.RcsDestination == pos);
        if (station is null)
            return null;

        if (!AgvStationArrivalHelper.IsAgvArrivedForStationUi(agv))
            return null;

        _lastNavigatedPosition = pos;
        return station;
    }
}
