namespace AgvDispatch.Sdk.Operations;

public sealed class AgvReadinessResult
{
    public bool CanAcceptOrder { get; init; }
    public bool ShouldPauseForDoor { get; init; }
    public bool ShouldContinueAfterDoorClosed { get; init; }
    public bool RecommendsCancelOrder { get; init; }
    public bool HasActiveOrderBlockingNewOrder { get; init; }
    public AgvRecommendedAction RecommendedAction { get; init; }
    public string Reason { get; init; } = "";
}
