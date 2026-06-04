namespace WireCabinet.Hmi.Views;

/// <summary>MH 存料分步状态（与 OP 同规则）。</summary>
public sealed class MhStepFlow
{
    public int ActiveStepIndex { get; private set; }
    public int CompletedThrough { get; private set; } = -1;

    public bool IsStepActive(int index) => index == ActiveStepIndex;
    public bool IsStepFuture(int index) => index > ActiveStepIndex;

    public void CompleteStep(int index)
    {
        if (index > CompletedThrough) CompletedThrough = index;
        ActiveStepIndex = index + 1;
    }

    public void ResetFromStep(int index)
    {
        if (index <= ActiveStepIndex)
            ActiveStepIndex = index;
        if (index <= CompletedThrough + 1)
            CompletedThrough = index - 1;
    }

    public void ResetAll()
    {
        ActiveStepIndex = 0;
        CompletedThrough = -1;
    }
}
