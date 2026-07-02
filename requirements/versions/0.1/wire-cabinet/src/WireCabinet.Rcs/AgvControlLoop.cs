using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Operations;
using WireCabinet.Slots;

namespace WireCabinet.Rcs;

public sealed class AgvControlLoop
{
    private readonly IAgvDispatchClient _client;
    private readonly AgvDispatchOptions _options;
    private readonly IDoorStateProvider _doors;
    private readonly AgvOrderGate? _orderGate;
    private bool _pausedForDoor;
    private bool _callInProgress;

    public AgvControlLoop(IAgvDispatchClient client, AgvDispatchOptions options, IDoorStateProvider doors, AgvOrderGate? orderGate = null)
    {
        _client = client;
        _options = options;
        _doors = doors;
        _orderGate = orderGate;
    }

    public async Task<AgvLoopTickResult> TickAsync(int? dispatchDestinationIfAllowed = null, CancellationToken ct = default)
    {
        var snapshot = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
        // 订单已在 RCS 侧结束/取消后，清除“呼叫进行中”门禁，否则无法二次下单。
        if (string.IsNullOrWhiteSpace(snapshot.Vehicle.OrderTaskId))
            _callInProgress = false;

        var doorInput = await _doors.GetDoorStateAsync(ct).ConfigureAwait(false);
        var eval = AgvReadinessPolicy.Evaluate(snapshot.Vehicle, snapshot.Order, doorInput, _options.StateThresholds, _pausedForDoor, _callInProgress);

        var result = new AgvLoopTickResult(snapshot, doorInput, eval);

        if (eval.RecommendsCancelOrder && !string.IsNullOrWhiteSpace(snapshot.Vehicle.OrderTaskId))
        {
            await _client.CancelOrderAsync(snapshot.Vehicle.OrderTaskId, ct).ConfigureAwait(false);
            _callInProgress = false;
            return result with { ExecutedAction = "CancelOrder" };
        }

        if (eval.ShouldPauseForDoor && !_pausedForDoor)
        {
            await _client.PauseMovementAsync(cancellationToken: ct).ConfigureAwait(false);
            _pausedForDoor = true;
            return result with { ExecutedAction = "PauseMovement" };
        }

        if (eval.ShouldContinueAfterDoorClosed && _pausedForDoor)
        {
            await _client.ContinueMovementAsync(cancellationToken: ct).ConfigureAwait(false);
            _pausedForDoor = false;
            return result with { ExecutedAction = "ContinueMovement" };
        }

        if (dispatchDestinationIfAllowed is > 0 && eval.CanAcceptOrder && !_callInProgress
            && (_orderGate is null || !_orderGate.BlocksNewOrder(snapshot, AgvPendingOrderKind.Move)))
        {
            var created = await _client.CreateMoveOrderAsync(dispatchDestinationIfAllowed.Value, cancellationToken: ct).ConfigureAwait(false);
            _callInProgress = true;
            _orderGate?.RecordPlacedOrder(created.OrderId, AgvPendingOrderKind.Move);
            return result with { ExecutedAction = "CreateMoveOrder", CreatedOrderId = created.OrderId };
        }

        return result;
    }

    public bool CallInProgress => _callInProgress;

    public void ResetCallState() => _callInProgress = false;
    public void MarkCallInProgress() => _callInProgress = true;
}

public sealed record AgvLoopTickResult(
    AgvDispatch.Sdk.Models.VehicleSnapshot Snapshot,
    DoorStateInput Doors,
    AgvReadinessResult Evaluation)
{
    public string? ExecutedAction { get; init; }
    public string? CreatedOrderId { get; init; }
}
