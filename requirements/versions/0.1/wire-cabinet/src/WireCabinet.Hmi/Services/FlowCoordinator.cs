using WireCabinet.Flows;
using WireCabinet.Hmi.Views;

namespace WireCabinet.Hmi.Services;

public sealed class FlowCoordinator
{
    private readonly AppBootstrap _app;
    private FlowSessionRunner? _runner;
    private FlowDefinition? _flow;

    public FlowCoordinator(AppBootstrap app) => _app = app;

    public bool TryBeginFlow(string flowId, out string message)
    {
        if (!_app.WireSession.TryBegin(flowId, out message))
            return false;

        _flow = _app.Flows.FirstOrDefault(f => f.FlowId == flowId);
        if (_flow is null)
        {
            message = $"未找到流程 {flowId}";
            _app.WireSession.End();
            return false;
        }

        _runner = new FlowSessionRunner(_app.EngineServices);
        _runner.Begin(_flow);
        message = "";
        return true;
    }

    public void EndFlow()
    {
        _runner = null;
        _flow = null;
        _app.WireSession.End();
    }

    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceUntilPause(IDictionary<string, string>? userInput = null)
    {
        if (_runner is null)
            return (false, "流程未开始", FlowPauseReason.Error);

        if (userInput is not null && _runner.Engine.CurrentNodeId is not null)
            _runner.SetUserInput(_runner.Engine.CurrentNodeId, userInput);

        var entry = _runner.RunUntilPause();
        if (_runner.PauseReason == FlowPauseReason.Error)
            return (false, _runner.PauseMessage ?? entry?.Result ?? "流程错误", FlowPauseReason.Error);

        if (_runner.PauseReason == FlowPauseReason.Terminal)
        {
            EndFlow();
            return (true, _runner.PauseMessage ?? "流程完成", FlowPauseReason.Terminal);
        }

        return (true, _runner.PauseMessage ?? "等待输入", _runner.PauseReason);
    }

    public string? CurrentNodeId => _runner?.Engine.CurrentNodeId;

    public object? GetField(string nodeFieldRef)
    {
        if (_runner?.Engine.Context is null) return null;
        return _runner.Engine.Context.GetRef(nodeFieldRef);
    }

    public void FillOpStep0(string opId, string workGroup, string shift) =>
        _runner?.SetUserInput("inputOperatorWorkInfo", new Dictionary<string, string>
        {
            ["operator_id"] = opId,
            ["work_group"] = workGroup,
            ["shift"] = shift
        });

    public void FillMhLotNo(string lotNo) =>
        _runner?.SetUserInput("inputAvailableWireLotNo", new Dictionary<string, string> { ["wire_lot_no"] = lotNo });
}
