using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Models;
using AgvDispatch.Sdk.Operations;

namespace WireCabinet.Rcs;

/// <summary>0.1：充电中仍允许控车（放宽 Sample 对 CHARGING 的早退）。</summary>
public static class AgvReadinessPolicy
{
    public static AgvReadinessResult Evaluate(
        VehicleInfoDto vehicle,
        OrderDetailDto? order,
        DoorStateInput doors,
        AgvStateThresholds thresholds,
        bool previouslyPausedForDoor,
        bool callInProgress)
    {
        var baseResult = AgvOperationEvaluator.Evaluate(vehicle, order, doors, thresholds, previouslyPausedForDoor, callInProgress);
        if (vehicle.SysState is null || !vehicle.SysState.Equals("CHARGING", StringComparison.OrdinalIgnoreCase))
            return baseResult;

        if (doors.AnyDoorOpen || previouslyPausedForDoor)
            return baseResult;

        return new AgvReadinessResult
        {
            CanAcceptOrder = !callInProgress,
            ShouldPauseForDoor = baseResult.ShouldPauseForDoor,
            ShouldContinueAfterDoorClosed = baseResult.ShouldContinueAfterDoorClosed,
            RecommendsCancelOrder = baseResult.RecommendsCancelOrder,
            HasActiveOrderBlockingNewOrder = callInProgress,
            RecommendedAction = callInProgress ? AgvRecommendedAction.None : AgvRecommendedAction.CreateMoveOrder,
            Reason = callInProgress ? "充电中，已有订单执行中" : "充电中，允许手动控车离站/移站"
        };
    }
}
