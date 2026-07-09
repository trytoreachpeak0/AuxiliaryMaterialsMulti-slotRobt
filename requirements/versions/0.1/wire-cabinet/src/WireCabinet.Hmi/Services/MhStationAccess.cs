namespace WireCabinet.Hmi.Services;

/// <summary>MH 存料流程到站门禁：物料员页面已不限制必须在物料间站点，仅校验车辆已到站、非充电中。</summary>
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

        var (isMoving, isCharging) = App.UiGate.GetVehicleMotionState();
        if (isCharging)
            return (false, "充电中禁止焊丝开锁与写库。");
        if (isMoving)
            return (false, "车辆移动中，请等待到站。");

        return AgvStationArrivalHelper.IsAgvArrivedForStationUi(App.Agv)
            ? (true, "")
            : (false, "请先等待车辆到站。");
    }
}
