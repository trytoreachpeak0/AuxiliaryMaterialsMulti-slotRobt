namespace WireDispenser.Demo.Views;

/// <summary>
/// OP 分步使能：仅当前步可编辑；通过后锁定；清空某步主输入则回退并清空后续步。
/// </summary>
public sealed class OpStepFlow
{
    public const int StepCount = 6;

    /// <summary>已完成并锁定的最大步骤索引，-1 表示尚未完成任何步。</summary>
    public int CompletedThrough { get; private set; } = -1;

    public int ActiveStepIndex => CompletedThrough + 1 < StepCount ? CompletedThrough + 1 : StepCount - 1;

    public bool IsStepLocked(int stepIndex) => stepIndex <= CompletedThrough;

    public bool IsStepActive(int stepIndex) => stepIndex == ActiveStepIndex;

    public bool IsStepFuture(int stepIndex) => stepIndex > ActiveStepIndex;

    public void CompleteStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= StepCount)
            return;
        if (stepIndex != CompletedThrough + 1)
            return;
        CompletedThrough = stepIndex;
    }

    public void ResetFromStep(int stepIndex)
    {
        if (stepIndex < 0)
            stepIndex = 0;
        CompletedThrough = stepIndex - 1;
    }

    public void ResetAll() => CompletedThrough = -1;
}
