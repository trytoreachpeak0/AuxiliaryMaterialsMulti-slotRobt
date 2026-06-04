namespace AgvDispatch.Sdk.Operations;

public enum AgvRecommendedAction
{
    PollOnly,
    CancelOrder,
    PauseMovement,
    ContinueMovement,
    CreateMoveOrder,
    CancelEmergency,
    None
}
