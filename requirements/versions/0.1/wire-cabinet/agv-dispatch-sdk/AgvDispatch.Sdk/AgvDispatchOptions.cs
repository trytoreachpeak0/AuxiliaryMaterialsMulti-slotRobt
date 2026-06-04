namespace AgvDispatch.Sdk;

/// <summary>
/// SDK 全量配置（禁止在代码中写死环境相关常量）。
/// </summary>
public sealed class AgvDispatchOptions
{
    public const string SectionName = "AgvDispatch";

    /// <summary>调度 API 根地址，如 http://host:8888（无尾部斜杠）。</summary>
    public string BaseUrl { get; set; } = "";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>默认车辆 deviceKey（原 SBcon / RobotInfo.ID）。</summary>
    public string DefaultDeviceKey { get; set; } = "";

    public int DefaultMapId { get; set; }

    /// <summary>下单默认订单名（原 order006）。</summary>
    public string DefaultOrderName { get; set; } = "order006";

    /// <summary>查询多车列表时的 deviceId 列表（原 49,48）。</summary>
    public int[] VehicleQueryDeviceIds { get; set; } = Array.Empty<int>();

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMilliseconds(9600);

    public int ChargeDestination { get; set; }
    public int ChargeActionId { get; set; }
    public string ChargeActionName { get; set; } = "充电";
    public int ChargeActionParam1 { get; set; } = 1;
    public int ChargeActionParam2 { get; set; } = 0;

    /// <summary>判断充电桩占用：终点站名包含即视为占用。</summary>
    public IList<string> ChargeOccupiedStationNames { get; set; } = new List<string>();

    /// <summary>判断充电桩占用：当前位置站点号。</summary>
    public IList<int> ChargeOccupiedPositions { get; set; } = new List<int>();

    public long PauseMovementMessageId { get; set; }
    public long ContinueMovementMessageId { get; set; }
    public long CancelEmergencyMessageId { get; set; } = 868368;

    public AgvStateThresholds StateThresholds { get; set; } = new();

    /// <summary>推荐轮询间隔（文档/LLM 用，SDK 不强制）。</summary>
    public int RecommendedPollIntervalMs { get; set; } = 500;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException($"{nameof(BaseUrl)} is required.");
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException($"{nameof(Username)} and {nameof(Password)} are required.");
        if (string.IsNullOrWhiteSpace(DefaultDeviceKey))
            throw new InvalidOperationException($"{nameof(DefaultDeviceKey)} is required.");
        if (VehicleQueryDeviceIds is null || VehicleQueryDeviceIds.Length == 0)
            throw new InvalidOperationException($"{nameof(VehicleQueryDeviceIds)} must contain at least one id.");
    }
}
