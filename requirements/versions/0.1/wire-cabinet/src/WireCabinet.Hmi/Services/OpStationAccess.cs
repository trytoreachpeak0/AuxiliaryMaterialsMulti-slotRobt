namespace WireCabinet.Hmi.Services;

/// <summary>OP 存取流程到站门禁。</summary>
public static class OpStationAccess
{
    public const string FlowId = "operator_return_wire_and_issue_available_wire";

    public static async Task<(bool Allowed, string Message)> CanRunOpFlowAsync()
    {
        if (App.UiGate.SkipStationGates)
            return (true, "");

        if (!App.Agv.IsConfigured)
            return (true, "到站校验已跳过（RCS 未配置）。");

        if (!App.Agv.IsConnected && !await App.Agv.ConnectAsync())
            return (false, App.Agv.LastError ?? "RCS 未连接，无法校验到站。");

        await App.Agv.RefreshStatusAsync();

        if (App.StationGate is null)
            return App.UiGate.CanUseOpPage
                ? (true, App.UiGate.GetStationBlockReason("OP"))
                : (false, App.UiGate.GetStationBlockReason("OP"));

        var (isMoving, isCharging) = App.UiGate.GetVehicleMotionState();
        var pos = App.Agv.LastPosition;
        var isArrived = App.UiGate.IsAtRoleStation("OP")
                        && AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv);

        return App.StationGate.CanRunWireFlow(
            "OP",
            FlowId,
            isArrived: isArrived,
            currentPosition: pos > 0 ? pos : null,
            isMoving: isMoving,
            isCharging: isCharging);
    }
}
