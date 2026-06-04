using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Operations;

namespace AgvDispatch.Sample.Integration;

/// <summary>
/// 供其他项目复制的主循环：轮询车状态 + 门状态 → 评估 → 调用调度 API。
/// 仓门状态通过 <see cref="IDoorStateProvider"/> 注入（0.1 生产：格口控制层聚合 DI+DB；勿依赖旧 closeFlag 位串）。
/// </summary>
public sealed class AgvControlLoop
{
    private readonly IAgvDispatchClient _client;
    private readonly AgvDispatchOptions _options;
    private readonly IDoorStateProvider _doors;
    private readonly IAgvDispatchCommandSink? _sink;

    private bool _pausedForDoor;
    private bool _callInProgress;

    public AgvControlLoop(
        IAgvDispatchClient client,
        AgvDispatchOptions options,
        IDoorStateProvider doors,
        IAgvDispatchCommandSink? sink = null)
    {
        _client = client;
        _options = options;
        _doors = doors;
        _sink = sink;
    }

    /// <summary>单次 tick，可在 Timer / BackgroundService 中调用。</summary>
    public async Task<AgvLoopTickResult> TickAsync(
        int? dispatchDestinationIfAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _client.GetVehicleSnapshotAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var doorInput = await _doors.GetDoorStateAsync(cancellationToken).ConfigureAwait(false);
        var eval = AgvOperationEvaluator.Evaluate(
            snapshot.Vehicle,
            snapshot.Order,
            doorInput,
            _options.StateThresholds,
            previouslyPausedForDoor: _pausedForDoor,
            callInProgress: _callInProgress);

        var result = new AgvLoopTickResult(snapshot, doorInput, eval);
        _sink?.OnEvaluated(result);

        if (eval.RecommendsCancelOrder && !string.IsNullOrWhiteSpace(snapshot.Vehicle.OrderTaskId))
        {
            await _client.CancelOrderAsync(snapshot.Vehicle.OrderTaskId, cancellationToken).ConfigureAwait(false);
            _callInProgress = false;
            _sink?.OnCanceled(snapshot.Vehicle.OrderTaskId);
            return result with { ExecutedAction = "CancelOrder" };
        }

        if (eval.ShouldPauseForDoor)
        {
            await _client.PauseMovementAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _pausedForDoor = true;
            _sink?.OnPaused();
            return result with { ExecutedAction = "PauseMovement" };
        }

        if (eval.ShouldContinueAfterDoorClosed)
        {
            await _client.ContinueMovementAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _pausedForDoor = false;
            _sink?.OnContinued();
            return result with { ExecutedAction = "ContinueMovement" };
        }

        if (dispatchDestinationIfAllowed is > 0
            && eval.CanAcceptOrder
            && !_callInProgress)
        {
            var created = await _client.CreateMoveOrderAsync(
                dispatchDestinationIfAllowed.Value,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            _callInProgress = true;
            _sink?.OnOrderCreated(dispatchDestinationIfAllowed.Value, created.OrderId);
            return result with { ExecutedAction = "CreateMoveOrder", CreatedOrderId = created.OrderId };
        }

        return result;
    }

    public void ResetCallState() => _callInProgress = false;

    public void MarkCallInProgress() => _callInProgress = true;
}

public sealed record AgvLoopTickResult(
    Sdk.Models.VehicleSnapshot Snapshot,
    DoorStateInput Doors,
    AgvReadinessResult Evaluation)
{
    public string? ExecutedAction { get; init; }
    public string? CreatedOrderId { get; init; }
}

/// <summary>业务提供仓门状态（0.1：返回 DoorStateInput 布尔量；FromCloseFlag 仅 Sample 遗留）。</summary>
public interface IDoorStateProvider
{
    Task<DoorStateInput> GetDoorStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>可选日志/埋点。</summary>
public interface IAgvDispatchCommandSink
{
    void OnEvaluated(AgvLoopTickResult result);
    void OnPaused();
    void OnContinued();
    void OnCanceled(string orderId);
    void OnOrderCreated(int destination, string? orderId);
}

/// <summary>示例：固定 closeFlag 字符串。</summary>
public sealed class FixedCloseFlagDoorProvider : IDoorStateProvider
{
    private readonly Func<string?> _getFlag;
    private readonly AgvStateThresholds _thresholds;

    public FixedCloseFlagDoorProvider(Func<string?> getFlag, AgvStateThresholds thresholds)
    {
        _getFlag = getFlag;
        _thresholds = thresholds;
    }

    public Task<DoorStateInput> GetDoorStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(DoorStateInput.FromCloseFlag(_getFlag(), _thresholds));
}

public sealed class ConsoleAgvSink : IAgvDispatchCommandSink
{
    public void OnEvaluated(AgvLoopTickResult result) =>
        Console.WriteLine($"[eval] CanAccept={result.Evaluation.CanAcceptOrder} Rec={result.Evaluation.RecommendedAction} | {result.Evaluation.Reason}");

    public void OnPaused() => Console.WriteLine("[cmd] PauseMovementAsync");
    public void OnContinued() => Console.WriteLine("[cmd] ContinueMovementAsync");
    public void OnCanceled(string orderId) => Console.WriteLine($"[cmd] CancelOrderAsync {orderId}");
    public void OnOrderCreated(int destination, string? orderId) =>
        Console.WriteLine($"[cmd] CreateMoveOrderAsync dest={destination} orderId={orderId}");
}
