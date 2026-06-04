namespace WireCabinet.Slots;

public interface IWireOperationSession
{
    bool HasActiveSession { get; }
    string? ActiveFlowId { get; }
    bool TryBegin(string flowId, out string message);
    void End();
}

public sealed class WireOperationSession : IWireOperationSession
{
    private string? _flowId;

    public bool HasActiveSession => _flowId is not null;
    public string? ActiveFlowId => _flowId;

    public bool TryBegin(string flowId, out string message)
    {
        if (_flowId is not null && !string.Equals(_flowId, flowId, StringComparison.OrdinalIgnoreCase))
        {
            message = "请先完成或退出当前操作。";
            return false;
        }
        _flowId = flowId;
        message = "";
        return true;
    }

    public void End() => _flowId = null;
}
