namespace WireCabinet.Flows;

public enum FlowPauseReason
{
    None,
    UserInput,
    AwaitingAction,
    AwaitingDoorClose,
    Terminal,
    Error
}

/// <summary>驱动 FlowEngine 直至需 UI 暂停或流程结束。</summary>
public sealed class FlowSessionRunner
{
    private readonly FlowEngine _engine;

    public FlowSessionRunner(EngineServices services) => _engine = new FlowEngine(services);

    public FlowEngine Engine => _engine;
    public FlowPauseReason PauseReason { get; private set; }
    public string? PauseMessage { get; private set; }

    public void Begin(FlowDefinition flow)
    {
        _engine.Begin(flow);
        PauseReason = FlowPauseReason.None;
        PauseMessage = null;
    }

    public TraceEntry? RunUntilPause(string? pauseBeforeNodeId = null, bool pauseBeforeDoorClose = false)
    {
        if (_engine.Finished)
        {
            PauseReason = FlowPauseReason.Error;
            PauseMessage = "流程已结束，请重新开始。";
            return _engine.Trace.LastOrDefault();
        }

        while (!_engine.Finished)
        {
            var node = _engine.CurrentNode;
            if (node is null) break;

            if (pauseBeforeNodeId is not null
                && string.Equals(node.Id, pauseBeforeNodeId, StringComparison.OrdinalIgnoreCase))
            {
                PauseReason = FlowPauseReason.AwaitingAction;
                PauseMessage = node.Text;
                return null;
            }

            if (pauseBeforeDoorClose && node.Type == NodeType.SlotClose)
            {
                PauseReason = FlowPauseReason.AwaitingDoorClose;
                PauseMessage = node.Text;
                return null;
            }

            if (node.Type == NodeType.UserInput)
            {
                var hasInput = _engine.Context?.UserInputs.ContainsKey(node.Id) == true;
                if (!hasInput)
                {
                    PauseReason = FlowPauseReason.UserInput;
                    PauseMessage = node.Text;
                    return null;
                }
            }

            var entry = _engine.StepOnce();
            var isErrorOutcome = entry.Status == Severity.Error
                || string.Equals(entry.Outcome, "error", StringComparison.OrdinalIgnoreCase);
            if (isErrorOutcome)
            {
                // error 已把 CurrentNodeId 指到 routing 目标；若目标是 terminal 且尚未执行，再步进一次
                if (!_engine.Finished && _engine.CurrentNode?.Type == NodeType.Terminal)
                {
                    entry = _engine.StepOnce();
                    if (_engine.Finished || string.Equals(entry.NodeType, "terminal", StringComparison.OrdinalIgnoreCase))
                    {
                        PauseReason = FlowPauseReason.Terminal;
                        PauseMessage = entry.Result;
                        return entry;
                    }
                }

                PauseReason = FlowPauseReason.Error;
                PauseMessage = entry.Result;
                return entry;
            }

            // user_input 已消费后清除，避免重复暂停在同一输入节点
            if (node.Type == NodeType.UserInput)
                _engine.Context?.UserInputs.Remove(node.Id);

            if (_engine.Finished || entry.NodeType == "terminal")
            {
                PauseReason = FlowPauseReason.Terminal;
                PauseMessage = entry.Result;
                return entry;
            }
        }

        PauseReason = FlowPauseReason.Terminal;
        return _engine.Trace.LastOrDefault();
    }

    public void SetUserInput(string nodeId, IDictionary<string, string> fields)
    {
        if (_engine.Context is null) return;
        _engine.Context.UserInputs[nodeId] = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
    }

    public void ResetUserInputs() => _engine.Context?.UserInputs.Clear();

    /// <summary>批号校验失败后回到归还批号输入节点，允许同一会话内重新查询。</summary>
    public void RewindToNode(string nodeId)
    {
        _engine.RewindToNode(nodeId);
        _engine.Context?.UserInputs.Remove(nodeId);
        PauseReason = FlowPauseReason.None;
        PauseMessage = null;
    }
}
