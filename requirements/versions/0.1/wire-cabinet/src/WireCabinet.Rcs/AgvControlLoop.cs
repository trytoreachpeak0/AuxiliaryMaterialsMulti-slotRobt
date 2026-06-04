using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Operations;
using WireCabinet.Slots;

namespace WireCabinet.Rcs;

public sealed class AgvControlLoop
{
    private readonly IAgvDispatchClient _client;
    private readonly AgvDispatchOptions _options;
    private readonly IDoorStateProvider _doors;
    private bool _pausedForDoor;
    private bool _callInProgress;

    public AgvControlLoop(IAgvDispatchClient client, AgvDispatchOptions options, IDoorStateProvider doors)
    {
        _client = client;
        _options = options;
        _doors = doors;
    }

    public async Task<AgvLoopTickResult> TickAsync(int? dispatchDestinationIfAllowed = null, CancellationToken ct = default)
    {
        var snapshot = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
        var doorInput = await _doors.GetDoorStateAsync(ct).ConfigureAwait(false);
        var eval = AgvReadinessPolicy.Evaluate(snapshot.Vehicle, snapshot.Order, doorInput, _options.StateThresholds, _pausedForDoor, _callInProgress);

        var result = new AgvLoopTickResult(snapshot, doorInput, eval);

        if (eval.RecommendsCancelOrder && !string.IsNullOrWhiteSpace(snapshot.Vehicle.OrderTaskId))
        {
            await _client.CancelOrderAsync(snapshot.Vehicle.OrderTaskId, ct).ConfigureAwait(false);
            _callInProgress = false;
            return result with { ExecutedAction = "CancelOrder" };
        }

        if (eval.ShouldPauseForDoor)
        {
            await _client.PauseMovementAsync(cancellationToken: ct).ConfigureAwait(false);
            _pausedForDoor = true;
            return result with { ExecutedAction = "PauseMovement" };
        }

        if (eval.ShouldContinueAfterDoorClosed)
        {
            await _client.ContinueMovementAsync(cancellationToken: ct).ConfigureAwait(false);
            _pausedForDoor = false;
            return result with { ExecutedAction = "ContinueMovement" };
        }

        if (dispatchDestinationIfAllowed is > 0 && eval.CanAcceptOrder && !_callInProgress)
        {
            var created = await _client.CreateMoveOrderAsync(dispatchDestinationIfAllowed.Value, cancellationToken: ct).ConfigureAwait(false);
            _callInProgress = true;
            return result with { ExecutedAction = "CreateMoveOrder", CreatedOrderId = created.OrderId };
        }

        return result;
    }

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
