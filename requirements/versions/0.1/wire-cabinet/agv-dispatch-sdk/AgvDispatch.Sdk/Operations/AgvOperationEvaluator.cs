using AgvDispatch.Sdk.Models;

namespace AgvDispatch.Sdk.Operations;

/// <summary>
/// 从 MainFrm getVehstatus / AGVWork 提取的可配置运行规则（纯逻辑）。
/// </summary>
public static class AgvOperationEvaluator
{
    public static AgvReadinessResult Evaluate(
        VehicleInfoDto vehicle,
        OrderDetailDto? order,
        DoorStateInput doors,
        AgvStateThresholds thresholds,
        bool previouslyPausedForDoor = false,
        bool callInProgress = false)
    {
        var reasons = new List<string>();
        var action = AgvRecommendedAction.PollOnly;

        if (!string.IsNullOrWhiteSpace(vehicle.OrderTaskId))
        {
            if (order != null)
            {
                var state = order.EffectiveOrderState;
                if (state == thresholds.OrderStateCanceled)
                {
                    return new AgvReadinessResult
                    {
                        RecommendsCancelOrder = true,
                        RecommendedAction = AgvRecommendedAction.CancelOrder,
                        Reason = $"订单 state={state}，需取消后恢复。"
                    };
                }

                if (state == thresholds.OrderStatePending)
                {
                    return new AgvReadinessResult
                    {
                        HasActiveOrderBlockingNewOrder = true,
                        RecommendedAction = AgvRecommendedAction.PollOnly,
                        Reason = "任务已下，等待执行，禁止新下单。"
                    };
                }
            }

            return new AgvReadinessResult
            {
                HasActiveOrderBlockingNewOrder = true,
                RecommendedAction = AgvRecommendedAction.PollOnly,
                Reason = "存在进行中订单。"
            };
        }

        var online = IsOnline(vehicle, thresholds);
        if (!online)
        {
            return Fail("车辆未在线或不可调度。", AgvRecommendedAction.PollOnly);
        }

        var sys = vehicle.SysState ?? "";
        if (thresholds.ChargingSysStates.Contains(sys, StringComparer.OrdinalIgnoreCase))
        {
            return Fail("充电中，不可下单。", AgvRecommendedAction.PollOnly);
        }

        if (thresholds.ErrorSysStates.Contains(sys, StringComparer.OrdinalIgnoreCase))
        {
            return new AgvReadinessResult
            {
                RecommendedAction = AgvRecommendedAction.CancelEmergency,
                Reason = "系统 ERROR，考虑 CancelEmergencyAsync。"
            };
        }

        var nonIdle = thresholds.NonIdleSysStatesForDoorPause.Contains(sys, StringComparer.OrdinalIgnoreCase)
                      || !thresholds.IdleSysStates.Contains(sys, StringComparer.OrdinalIgnoreCase);

        if (nonIdle && doors.AnyDoorOpen)
        {
            return new AgvReadinessResult
            {
                ShouldPauseForDoor = true,
                RecommendedAction = AgvRecommendedAction.PauseMovement,
                Reason = "车辆非空闲且仓门打开，应暂停。"
            };
        }

        if (doors.AllDoorsClosed && previouslyPausedForDoor)
        {
            return new AgvReadinessResult
            {
                ShouldContinueAfterDoorClosed = true,
                RecommendedAction = AgvRecommendedAction.ContinueMovement,
                Reason = "仓门已全关，应继续任务。"
            };
        }

        var move = vehicle.EffectiveMoveState ?? "";
        var proc = vehicle.ProcState ?? "";
        var moveOk = thresholds.ReadyMoveStates.Contains(move, StringComparer.OrdinalIgnoreCase);
        var procOk = thresholds.ReadyProcStates.Contains(proc, StringComparer.OrdinalIgnoreCase);
        var idle = thresholds.IdleSysStates.Contains(sys, StringComparer.OrdinalIgnoreCase);

        var canAccept = idle && moveOk && procOk && doors.AllDoorsClosed && !callInProgress;

        if (canAccept)
        {
            action = AgvRecommendedAction.CreateMoveOrder;
            reasons.Add("可接单：空闲、动作完结、进程空闲、仓门全关、未在呼叫中。");
        }
        else
        {
            if (!idle) reasons.Add($"sys_state={sys} 非空闲。");
            if (!moveOk) reasons.Add($"move/actionState={move} 未就绪。");
            if (!procOk) reasons.Add($"proc_state={proc} 忙碌。");
            if (!doors.AllDoorsClosed) reasons.Add("仓门未全关。");
            if (callInProgress) reasons.Add("呼叫进行中。");
        }

        return new AgvReadinessResult
        {
            CanAcceptOrder = canAccept,
            RecommendedAction = action,
            Reason = string.Join(" ", reasons)
        };
    }

    public static bool IsChargeStationOccupied(
        IEnumerable<VehicleListItemDto> vehicles,
        AgvDispatchOptions options)
    {
        foreach (var v in vehicles)
        {
            if (options.ChargeOccupiedPositions.Contains(v.CurrentPosition))
                return true;
            if (v.EndStationName != null &&
                options.ChargeOccupiedStationNames.Any(n =>
                    v.EndStationName.Contains(n, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static bool IsOnline(VehicleInfoDto vehicle, AgvStateThresholds thresholds)
    {
        if (vehicle.Enable)
            return true;
        var s = vehicle.OnlineAsString;
        return s != null && thresholds.OnlineTrueValues.Contains(s, StringComparer.OrdinalIgnoreCase);
    }

    private static AgvReadinessResult Fail(string reason, AgvRecommendedAction action) =>
        new() { Reason = reason, RecommendedAction = action };
}
