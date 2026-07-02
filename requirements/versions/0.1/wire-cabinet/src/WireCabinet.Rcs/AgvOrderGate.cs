using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Models;
namespace WireCabinet.Rcs;

public enum AgvPendingOrderKind
{
    Charge,
    Move
}

/// <summary>统一订单门禁：本地 pending + RCS 快照，防止队列等待期间重复下单。</summary>
public sealed class AgvOrderGate
{
    private readonly AgvStateThresholds _thresholds;
    private readonly Func<bool> _callInProgress;

    private string? _localPendingOrderId;
    private AgvPendingOrderKind? _localPendingKind;
    private OrderDetailDto? _cachedPendingDetail;

    public AgvOrderGate(AgvStateThresholds thresholds, Func<bool>? callInProgress = null)
    {
        _thresholds = thresholds;
        _callInProgress = callInProgress ?? (() => false);
    }

    public string? LocalPendingOrderId => _localPendingOrderId;
    public AgvPendingOrderKind? LocalPendingKind => _localPendingKind;

    public void RecordPlacedOrder(string? orderId, AgvPendingOrderKind kind)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return;

        _localPendingOrderId = orderId;
        _localPendingKind = kind;
        _cachedPendingDetail = null;
    }

    public void ClearPending()
    {
        _localPendingOrderId = null;
        _localPendingKind = null;
        _cachedPendingDetail = null;
    }

    public bool HasBlockingOrder(VehicleSnapshot snap)
    {
        if (!string.IsNullOrWhiteSpace(snap.Vehicle.OrderTaskId))
            return true;

        if (!string.IsNullOrWhiteSpace(_localPendingOrderId))
            return true;

        return snap.Order is not null && IsBlockingOrderState(snap.Order.EffectiveOrderState);
    }

    public bool BlocksNewOrder(VehicleSnapshot snap, AgvPendingOrderKind? placingKind = null)
    {
        if (_callInProgress())
            return true;

        if (!_thresholds.IdleSysStates.Contains(snap.Vehicle.SysState ?? "", StringComparer.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(snap.Vehicle.OrderTaskId))
            return true;

        if (!string.IsNullOrWhiteSpace(_localPendingOrderId))
            return true;

        if (snap.Order is not null && IsBlockingOrderState(snap.Order.EffectiveOrderState))
            return true;

        if (placingKind == AgvPendingOrderKind.Charge
            && _thresholds.ChargingSysStates.Contains(snap.Vehicle.SysState ?? "", StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public void ApplySnapshot(VehicleSnapshot snap)
    {
        var taskId = snap.Vehicle.OrderTaskId;
        if (!string.IsNullOrWhiteSpace(taskId) && !string.IsNullOrWhiteSpace(_localPendingOrderId))
        {
            if (string.Equals(taskId, _localPendingOrderId, StringComparison.OrdinalIgnoreCase))
                ClearPending();
            else
                ClearPending();
        }
    }

    public async Task ReconcilePendingAsync(IAgvDispatchClient client, VehicleSnapshot snap, CancellationToken ct = default)
    {
        ApplySnapshot(snap);

        if (!string.IsNullOrWhiteSpace(snap.Vehicle.OrderTaskId))
            return;

        if (string.IsNullOrWhiteSpace(_localPendingOrderId))
            return;

        try
        {
            var detail = await client.GetOrderDetailAsync(_localPendingOrderId, ct).ConfigureAwait(false);
            _cachedPendingDetail = detail;
            if (ShouldClearPending(detail))
                ClearPending();
        }
        catch
        {
            // 查询失败时保留 pending，继续阻塞重复下单。
        }
    }

    public string FormatOrderStateDisplay(VehicleSnapshot snap)
    {
        if (snap.Order is not null)
        {
            var state = snap.Order.EffectiveOrderState;
            if (state == _thresholds.OrderStatePending)
                return "队列中";
            if (state == _thresholds.OrderStateCanceled)
                return "已取消（待清理）";

            var move = snap.Vehicle.EffectiveMoveState ?? "";
            if (move.Contains("PAUSED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(snap.Vehicle.SysState, "PAUSE", StringComparison.OrdinalIgnoreCase))
                return "已暂停";

            return "执行中";
        }

        if (!string.IsNullOrWhiteSpace(_localPendingOrderId))
            return "队列中";

        return "—";
    }

    public string FormatTaskSummary(VehicleSnapshot snap, Func<int, string> formatPosition)
    {
        var v = snap.Vehicle;
        if (!string.IsNullOrWhiteSpace(v.OrderTaskId))
            return FormatRcsTask(v, snap.Order, formatPosition, _thresholds.OrderStatePending);

        if (!string.IsNullOrWhiteSpace(_localPendingOrderId))
        {
            var kind = _localPendingKind == AgvPendingOrderKind.Charge ? "充电单" : "移动单";
            return $"{kind} {_localPendingOrderId}（队列中，等待执行）";
        }

        return "无进行中任务";
    }

    public bool CanCancelOrder(VehicleSnapshot snap)
    {
        if (!string.IsNullOrWhiteSpace(snap.Vehicle.OrderTaskId))
        {
            var v = snap.Vehicle;
            if (v.EffectiveMoveState?.Contains("PAUSED", StringComparison.OrdinalIgnoreCase) ?? false)
                return true;
            if (string.Equals(v.SysState, "PAUSE", StringComparison.OrdinalIgnoreCase))
                return true;
            if (snap.Order is not null && snap.Order.EffectiveOrderState == _thresholds.OrderStatePending)
                return true;
            return false;
        }

        return !string.IsNullOrWhiteSpace(_localPendingOrderId);
    }

    public string? ResolveCancelOrderId(VehicleSnapshot snap) =>
        !string.IsNullOrWhiteSpace(snap.Vehicle.OrderTaskId)
            ? snap.Vehicle.OrderTaskId
            : _localPendingOrderId;

    private static string FormatRcsTask(VehicleInfoDto v, OrderDetailDto? order, Func<int, string> formatPosition, int orderStatePending)
    {
        var dest = order?.Mission?.FirstOrDefault(m =>
            string.Equals(m.Type, "move", StringComparison.OrdinalIgnoreCase))?.Destination;
        if (dest is null or 0)
            dest = v.EndStationNo > 0 ? v.EndStationNo : null;

        var name = !string.IsNullOrWhiteSpace(v.EndStationName)
            ? v.EndStationName
            : dest is int d ? formatPosition(d) : "—";

        var orderLabel = string.IsNullOrWhiteSpace(v.OrderName) ? v.OrderTaskId : $"{v.OrderName}（{v.OrderTaskId}）";
        var queueHint = order?.EffectiveOrderState == orderStatePending ? "（队列中）" : "";

        return dest is int station
            ? $"{orderLabel}{queueHint} → {name}（站点 {station}）"
            : $"{orderLabel}{queueHint} → {name}";
    }

    private bool IsBlockingOrderState(int state) =>
        state == _thresholds.OrderStatePending || state == _thresholds.OrderStateCanceled;

    private bool ShouldClearPending(OrderDetailDto detail) =>
        !string.IsNullOrWhiteSpace(detail.DoneTime)
        || detail.EffectiveOrderState == _thresholds.OrderStateCanceled;
}
