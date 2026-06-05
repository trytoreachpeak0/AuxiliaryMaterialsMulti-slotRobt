namespace WireCabinet.Hmi.Services;

/// <summary>MH 存料流程到站门禁（需求 §2.2）。</summary>
public static class MhStationAccess
{
    public const string LoadFlowId = "material_handler_load_available_wire";

    public static async Task<(bool Allowed, string Message)> CanRunLoadWireFlowAsync()
    {
        if (App.UiGate.SkipStationGates)
            return (true, "");

        if (!App.Agv.IsConfigured)
            return (true, "到站校验已跳过（RCS 未配置）。");

        if (!App.Agv.IsConnected && !await App.Agv.ConnectAsync())
            return (false, App.Agv.LastError ?? "RCS 未连接，无法校验到站。");

        await App.Agv.RefreshStatusAsync();

        if (App.StationGate is null)
            return App.UiGate.CanUseMhPage
                ? (true, App.UiGate.GetStationBlockReason("MH"))
                : (false, App.UiGate.GetStationBlockReason("MH"));

        var (isMoving, isCharging) = App.UiGate.GetVehicleMotionState();
        var pos = App.Agv.LastPosition;
        var isArrived = App.UiGate.IsAtRoleStation("MH")
                        && AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv);

        return App.StationGate.CanRunWireFlow(
            "MH",
            LoadFlowId,
            isArrived: isArrived,
            currentPosition: pos > 0 ? pos : null,
            isMoving: isMoving,
            isCharging: isCharging);
    }
}
