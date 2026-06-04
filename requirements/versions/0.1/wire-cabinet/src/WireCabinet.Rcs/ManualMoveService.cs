using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Operations;
using WireCabinet.Slots;

namespace WireCabinet.Rcs;

public enum MoveUiState { Idle, Sending, Moving, Arrived, Failed, Timeout }

public sealed class ManualMoveService
{
    private readonly IAgvDispatchClient _client;
    private readonly AgvDispatchOptions _options;
    private readonly ISlotControlService _slots;
    private readonly AgvControlLoop _loop;

    public MoveUiState State { get; private set; } = MoveUiState.Idle;
    public int? TargetDestination { get; private set; }
    public string? LastMessage { get; private set; }

    public ManualMoveService(IAgvDispatchClient client, AgvDispatchOptions options, ISlotControlService slots, IDoorStateProvider doors)
    {
        _client = client;
        _options = options;
        _slots = slots;
        _loop = new AgvControlLoop(client, options, doors);
    }

    public async Task<(bool Ok, string Message)> RequestMoveAsync(int destination, CancellationToken ct = default)
    {
        if (_slots.HasUnlockInProgress() || !_slots.AreAllDoorsClosed())
            return (false, "存在开锁中或门未全关，禁止下发移动单。");

        TargetDestination = destination;
        State = MoveUiState.Sending;
        var tick = await _loop.TickAsync(destination, ct).ConfigureAwait(false);
        if (tick.Evaluation.CanAcceptOrder)
        {
            State = MoveUiState.Moving;
            LastMessage = "已下发移动单";
            return (true, LastMessage);
        }

        State = MoveUiState.Failed;
        LastMessage = tick.Evaluation.Reason;
        return (false, LastMessage);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var snap = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
        var pos = snap.Vehicle.CurrentPosition;
        if (TargetDestination is > 0 && pos == TargetDestination)
        {
            State = MoveUiState.Arrived;
            LastMessage = "已到站";
        }
        else if (!string.IsNullOrEmpty(snap.Vehicle.EffectiveMoveState) &&
                 !snap.Vehicle.EffectiveMoveState.Contains("idle", StringComparison.OrdinalIgnoreCase))
            State = MoveUiState.Moving;
    }
}
