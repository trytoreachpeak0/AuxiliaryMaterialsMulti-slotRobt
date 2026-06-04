namespace WireCabinet.Flows;

public enum FlowPauseReason
{
    None,
    UserInput,
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

    public TraceEntry? RunUntilPause()
    {
        while (!_engine.Finished)
        {
            var node = _engine.CurrentNode;
            if (node is null) break;

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
            if (entry.Status == Severity.Error || string.Equals(entry.Outcome, "error", StringComparison.OrdinalIgnoreCase))
            {
                PauseReason = FlowPauseReason.Error;
                PauseMessage = entry.Result;
                return entry;
            }

            if (_engine.Finished || node.Type == NodeType.Terminal)
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
}
