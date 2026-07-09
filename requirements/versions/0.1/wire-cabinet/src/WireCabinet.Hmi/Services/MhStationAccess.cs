namespace WireCabinet.Hmi.Services;

/// <summary>MH 存料流程到站门禁：物料员页面不限制必须在物料间站点，充电中也可操作，仅拦截车辆真正行驶中的情况。</summary>
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

        return App.UiGate.IsVehicleBusyForMh()
            ? (false, "车辆移动中，请等待到站。")
            : (true, "");
    }
}
