using WireCabinet.Rcs;

namespace WireCabinet.Hmi.Services;

/// <summary>集中 HMI 门控：AGV 任务、到站、手动移动锁定。</summary>
public sealed class HmiUiGateService
{
    private bool _movementControlsLocked;
    private bool _sawActiveOrderWhileLocked;

    public bool MovementControlsLocked => _movementControlsLocked;

    public void SetMovementLocked(bool locked)
    {
        _movementControlsLocked = locked;
        if (locked)
            _sawActiveOrderWhileLocked = false;
    }

    /// <summary>轮询刷新后调用：避免下单成功但 OrderTaskId 尚未出现时误解锁。</summary>
    public void OnAgvSnapshotRefreshed()
    {
        if (_movementControlsLocked && App.Agv.HasBlockingOrder)
            _sawActiveOrderWhileLocked = true;
    }

    public void TryClearMovementLock()
    {
        if (!_movementControlsLocked)
            return;
        if (App.Agv.HasBlockingOrder)
        {
            _sawActiveOrderWhileLocked = true;
            return;
        }

        if (!_sawActiveOrderWhileLocked)
            return;

        if (App.Agv.CanPlaceMoveOrChargeOrder)
            _movementControlsLocked = false;
    }

    public bool SkipStationGates =>
        App.StationGate is null || !App.Agv.IsConfigured;

    public bool BlocksSlotDoorControls
    {
        get
        {
            if (SkipStationGates)
                return false;
            return App.Agv.HasBlockingOrder || IsVehicleInTransit();
        }
    }

    public bool CanUseOpPage => CanUseRolePage("OP");

    public bool CanUseMhPage => CanUseRolePage("MH");

    public bool CanUseRolePage(string role)
    {
        if (SkipStationGates)
            return true;

        if (IsVehicleCharging())
            return false;

        // 物料员 MH 页面不再限制必须在物料间站点，任意站点均可使用（仍需已到站、非充电中）。
        if (string.Equals(role, "MH", StringComparison.OrdinalIgnoreCase))
            return AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv);

        if (!IsAtRoleStation(role))
            return false;

        return AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv);
    }

    public bool IsAtRoleStation(string role)
    {
        var pos = App.Agv.LastPosition;
        if (pos <= 0)
            return false;

        var station = ResolveWorkStationAt(pos);
        return station is not null
               && station.AllowedRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    public WorkStationConfig? ResolveWorkStationAt(int position) =>
        App.Agv.Stations.EnabledWorkStations.FirstOrDefault(s => s.RcsDestination == position);

    public string GetStationBlockReason(string role)
    {
        if (SkipStationGates)
            return "";

        if (IsVehicleCharging())
            return "充电中，请等待完成后再操作。";

        if (IsVehicleInTransit())
            return "车辆移动中，请等待到站。";

        var pos = App.Agv.LastPosition;
        if (pos <= 0)
            return $"请先移动至{RoleLabel(role)}作业站。";

        var station = ResolveWorkStationAt(pos);
        if (station is null)
            return $"当前站点 {pos} 不是已配置的作业站。";

        if (!station.AllowedRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
            return $"本站（{station.Name}）仅支持：{string.Join("/", station.AllowedRoles)}。";

        if (!AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv))
            return "车辆尚未停稳，请稍候。";

        return $"已到站：{station.Name}";
    }

    public (bool IsMoving, bool IsCharging) GetVehicleMotionState() =>
        (IsVehicleInTransit(), IsVehicleCharging());

    public bool IsVehicleInTransit()
    {
        if (App.Agv.Move?.State == MoveUiState.Moving)
            return true;

        if (App.Agv.HasBlockingOrder && !AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv))
            return true;

        if (AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv))
            return false;

        var move = App.Agv.LastMoveState ?? "";
        return !string.Equals(move, "0", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(move, "Idle", StringComparison.OrdinalIgnoreCase)
               && move != "—";
    }

    private static bool IsVehicleCharging() =>
        string.Equals(App.Agv.LastSysState, "CHARGING", StringComparison.OrdinalIgnoreCase);

    private static string RoleLabel(string role) =>
        string.Equals(role, "OP", StringComparison.OrdinalIgnoreCase) ? "操作员" : "物料员";
}
