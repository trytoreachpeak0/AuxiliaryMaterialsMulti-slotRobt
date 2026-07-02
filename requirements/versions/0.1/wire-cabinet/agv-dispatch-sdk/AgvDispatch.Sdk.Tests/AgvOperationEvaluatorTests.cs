using Xunit;
using AgvDispatch.Sdk.Models;
using AgvDispatch.Sdk.Operations;

namespace AgvDispatch.Sdk.Tests;

public class AgvOperationEvaluatorTests
{
    private static readonly AgvStateThresholds Thresholds = new();

    [Fact]
    public void ScenarioA_OrderState9_RecommendsCancel()
    {
        var vehicle = new VehicleInfoDto { OrderTaskId = "oid-1", Enable = true };
        var order = new OrderDetailDto { OrderState = 9 };
        var doors = DoorStateInput.FromCloseFlag(new string('1', 24), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, order, doors, Thresholds);

        Assert.True(r.RecommendsCancelOrder);
        Assert.Equal(AgvRecommendedAction.CancelOrder, r.RecommendedAction);
    }

    [Fact]
    public void ScenarioA_OrderState1_BlocksNewOrder()
    {
        var vehicle = new VehicleInfoDto { OrderTaskId = "oid-1", Enable = true };
        var order = new OrderDetailDto { OrderState = 1 };
        var doors = DoorStateInput.FromCloseFlag(new string('1', 24), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, order, doors, Thresholds);

        Assert.True(r.HasActiveOrderBlockingNewOrder);
        Assert.False(r.CanAcceptOrder);
    }

    [Fact]
    public void ScenarioB_CanAcceptOrder_WhenIdleAndDoorsClosed()
    {
        var vehicle = new VehicleInfoDto
        {
            Enable = true,
            SysState = "IDLE",
            ActionState = "AT_FINISHED",
            ProcState = "IDLE"
        };
        var doors = DoorStateInput.FromCloseFlag(new string('1', 24), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, null, doors, Thresholds, callInProgress: false);

        Assert.True(r.CanAcceptOrder);
        Assert.Equal(AgvRecommendedAction.CreateMoveOrder, r.RecommendedAction);
    }

    [Fact]
    public void ScenarioC_NonIdleAndDoorOpen_ShouldPause()
    {
        var vehicle = new VehicleInfoDto
        {
            Enable = true,
            SysState = "EXECUTING",
            ActionState = "AT_RUNNING",
            ProcState = "BUSY"
        };
        var doors = DoorStateInput.FromCloseFlag("10" + new string('1', 22), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, null, doors, Thresholds);

        Assert.True(r.ShouldPauseForDoor);
        Assert.Equal(AgvRecommendedAction.PauseMovement, r.RecommendedAction);
    }

    [Fact]
    public void ScenarioC_DoorsClosedAfterPause_ShouldContinue()
    {
        var vehicle = new VehicleInfoDto
        {
            Enable = true,
            SysState = "EXECUTING",
            ActionState = "AT_RUNNING"
        };
        var doors = DoorStateInput.FromCloseFlag(new string('1', 24), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, null, doors, Thresholds, previouslyPausedForDoor: true);

        Assert.True(r.ShouldContinueAfterDoorClosed);
    }

    [Fact]
    public void ScenarioC_ActiveMoveOrderAndDoorOpen_ShouldPause()
    {
        var vehicle = new VehicleInfoDto
        {
            OrderTaskId = "oid-move",
            Enable = true,
            SysState = "EXECUTING",
            ActionState = "AT_RUNNING"
        };
        var order = new OrderDetailDto { OrderState = 2 };
        var doors = DoorStateInput.FromCloseFlag("10" + new string('1', 22), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, order, doors, Thresholds);

        Assert.True(r.ShouldPauseForDoor);
        Assert.True(r.HasActiveOrderBlockingNewOrder);
    }

    [Fact]
    public void ScenarioC_ActiveMoveOrderDoorClosedAfterPause_ShouldContinue()
    {
        var vehicle = new VehicleInfoDto
        {
            OrderTaskId = "oid-move",
            Enable = true,
            SysState = "EXECUTING",
            ActionState = "AT_RUNNING"
        };
        var order = new OrderDetailDto { OrderState = 2 };
        var doors = DoorStateInput.FromCloseFlag(new string('1', 24), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, order, doors, Thresholds, previouslyPausedForDoor: true);

        Assert.True(r.ShouldContinueAfterDoorClosed);
    }

    [Fact]
    public void ScenarioC_ChargingAndDoorOpen_ShouldPause()
    {
        var vehicle = new VehicleInfoDto { Enable = true, SysState = "CHARGING" };
        var doors = DoorStateInput.FromCloseFlag("10" + new string('1', 22), Thresholds);

        var r = AgvOperationEvaluator.Evaluate(vehicle, null, doors, Thresholds);

        Assert.True(r.ShouldPauseForDoor);
    }
}
