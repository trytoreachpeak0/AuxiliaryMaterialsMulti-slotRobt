using AgvDispatch.Sdk;
using WireCabinet.Core.Config;

namespace WireCabinet.Rcs;

/// <summary>后台轮询电量，低电量下充电单，充满后回上一作业站。</summary>
public sealed class LowBatteryChargeService
{
    private readonly IAgvDispatchClient _client;
    private readonly AgvThresholdOptions _thresholds;
    private int? _lastWorkStationBeforeCharge;

    public LowBatteryChargeService(IAgvDispatchClient client, AgvThresholdOptions thresholds)
    {
        _client = client;
        _thresholds = thresholds;
    }

    public void RememberWorkStation(int destination) => _lastWorkStationBeforeCharge = destination;

    public async Task<string?> TryAutoChargeAsync(double batteryPercent, CancellationToken ct = default)
    {
        if (batteryPercent > _thresholds.LowBatteryPercent)
            return null;

        try
        {
            var order = await _client.CreateChargeOrderAsync(cancellationToken: ct).ConfigureAwait(false);
            return $"已自动下充电单：{order.OrderId}";
        }
        catch (Exception ex)
        {
            return $"回充下单失败：{ex.Message}";
        }
    }

    public async Task<string?> TryReturnToWorkStationAsync(double batteryPercent, CancellationToken ct = default)
    {
        if (batteryPercent < _thresholds.FullBatteryPercent || _lastWorkStationBeforeCharge is not int dest)
            return null;

        try
        {
            var order = await _client.CreateMoveOrderAsync(dest, cancellationToken: ct).ConfigureAwait(false);
            return $"充满后已返航作业站 {dest}，订单 {order.OrderId}";
        }
        catch (Exception ex)
        {
            return $"返航失败：{ex.Message}";
        }
    }
}
