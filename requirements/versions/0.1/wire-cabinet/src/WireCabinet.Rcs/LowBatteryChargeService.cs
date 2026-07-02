using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Models;
using WireCabinet.Core.Config;

namespace WireCabinet.Rcs;

/// <summary>后台轮询电量，低电量下充电单，充满后回作业站或默认物料间。</summary>
public sealed class LowBatteryChargeService
{
    private readonly IAgvDispatchClient _client;
    private readonly AgvDispatchOptions _options;
    private readonly AgvThresholdOptions _thresholds;
    private readonly AgvOrderGate _orderGate;
    private readonly StationConfiguration _stations;

    private bool _postChargeReturnPending;
    private int? _returnDestination;
    private int? _manualWorkStationHint;

    public LowBatteryChargeService(
        IAgvDispatchClient client,
        AgvDispatchOptions options,
        AgvThresholdOptions thresholds,
        AgvOrderGate orderGate,
        StationConfiguration stations)
    {
        _client = client;
        _options = options;
        _thresholds = thresholds;
        _orderGate = orderGate;
        _stations = stations;
    }

    public bool PostChargeReturnPending => _postChargeReturnPending;
    public int? ReturnDestination => _returnDestination;

    public void RememberWorkStation(int destination)
    {
        if (_stations.TryGetWorkStationAt(destination) is not null)
            _manualWorkStationHint = destination;
    }

    public void ArmPostChargeReturnToDefault()
    {
        var dest = SanitizeReturnDestination(_stations.ResolveDefaultReturnStation()?.RcsDestination);
        if (dest is null or <= 0)
            return;

        _returnDestination = dest;
        _postChargeReturnPending = true;
    }

    public void ClearPostChargeReturn()
    {
        _postChargeReturnPending = false;
        _returnDestination = null;
    }

    public void RememberReturnDestinationOnChargeDispatch(int currentPosition)
    {
        var dest = SanitizeReturnDestination(_stations.ResolveReturnDestination(currentPosition));
        if (dest is null or <= 0 && _manualWorkStationHint is int hint)
            dest = SanitizeReturnDestination(hint);

        if (dest is null or <= 0)
        {
            ClearPostChargeReturn();
            return;
        }

        _returnDestination = dest;
        _postChargeReturnPending = true;
    }

    public async Task<string?> TryAutoChargeAsync(VehicleSnapshot snap, CancellationToken ct = default)
    {
        var v = snap.Vehicle;
        if (v.Battery > _thresholds.LowBatteryPercent)
            return null;

        if (IsVehicleCharging(v))
            return null;

        if (_orderGate.BlocksNewOrder(snap, AgvPendingOrderKind.Charge))
            return null;

        if (await IsChargeStationOccupiedByOtherAsync(ct).ConfigureAwait(false))
            return null;

        try
        {
            var order = await _client.CreateChargeOrderAsync(cancellationToken: ct).ConfigureAwait(false);
            _orderGate.RecordPlacedOrder(order.OrderId, AgvPendingOrderKind.Charge);
            RememberReturnDestinationOnChargeDispatch(v.CurrentPosition);
            return $"已自动下充电单：{order.OrderId}";
        }
        catch (Exception ex)
        {
            return $"回充下单失败：{ex.Message}";
        }
    }

    public async Task<string?> TryReturnToWorkStationAsync(VehicleSnapshot snap, CancellationToken ct = default)
    {
        if (!_postChargeReturnPending)
            return null;

        if (snap.Vehicle.Battery < _thresholds.FullBatteryPercent)
            return null;

        var dest = SanitizeReturnDestination(_returnDestination);
        if (dest is null or <= 0)
        {
            ClearPostChargeReturn();
            return null;
        }

        if (snap.Vehicle.CurrentPosition == dest)
        {
            ClearPostChargeReturn();
            return null;
        }

        if (_orderGate.BlocksNewOrder(snap, AgvPendingOrderKind.Move))
            return null;

        var returnStation = dest.Value;
        try
        {
            var order = await _client.CreateMoveOrderAsync(returnStation, cancellationToken: ct).ConfigureAwait(false);
            _orderGate.RecordPlacedOrder(order.OrderId, AgvPendingOrderKind.Move);
            var name = _stations.TryGetWorkStationAt(returnStation)?.Name ?? returnStation.ToString();
            return $"充满后已返航 {name}（站点 {returnStation}），订单 {order.OrderId}";
        }
        catch (Exception ex)
        {
            return $"返航失败：{ex.Message}";
        }
    }

    public void BootstrapExternalChargeSession(VehicleSnapshot snap)
    {
        var v = snap.Vehicle;
        if (!IsVehicleCharging(v) && !_stations.IsChargeRelatedPosition(v.CurrentPosition))
            return;

        ArmPostChargeReturnToDefault();

        var defaultDest = SanitizeReturnDestination(_stations.ResolveDefaultReturnStation()?.RcsDestination);
        if (defaultDest is > 0
            && v.CurrentPosition == defaultDest
            && v.Battery >= _thresholds.FullBatteryPercent)
            ClearPostChargeReturn();
    }

    private bool IsVehicleCharging(VehicleInfoDto vehicle) =>
        _options.StateThresholds.ChargingSysStates.Contains(vehicle.SysState ?? "", StringComparer.OrdinalIgnoreCase);

    private int? SanitizeReturnDestination(int? destination)
    {
        if (destination is null or <= 0)
            return null;

        if (_stations.IsChargeRelatedPosition(destination.Value))
            return _stations.ResolveDefaultReturnStation()?.RcsDestination;

        return destination;
    }

    private async Task<bool> IsChargeStationOccupiedByOtherAsync(CancellationToken ct)
    {
        try
        {
            var vehicles = await _client.GetVehiclesAsync(cancellationToken: ct).ConfigureAwait(false);
            var ownKey = _options.DefaultDeviceKey;
            foreach (var v in vehicles)
            {
                if (!string.IsNullOrWhiteSpace(ownKey)
                    && string.Equals(v.DeviceKey, ownKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_options.ChargeOccupiedPositions.Contains(v.CurrentPosition))
                    return true;

                if (v.EndStationName != null
                    && _options.ChargeOccupiedStationNames.Any(n =>
                        v.EndStationName.Contains(n, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
