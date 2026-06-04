using System.Text.Json.Serialization;

namespace AgvDispatch.Sdk.Models;

/// <summary>
/// 单车状态（调度 getVehicleInfoByDeviceKey / 列表项共用字段）。
/// 常用字段见文档；其余为调度扩展字段。
/// </summary>
public class VehicleInfoDto
{
    [JsonPropertyName("deviceKey")]
    public string? DeviceKey { get; set; }

    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("deviceId")]
    public int DeviceId { get; set; }

    [JsonPropertyName("sysState")]
    public string? SysState { get; set; }

    [JsonPropertyName("locationState")]
    public string? LocationState { get; set; }

    [JsonPropertyName("orderTaskId")]
    public string? OrderTaskId { get; set; }

    [JsonPropertyName("battery")]
    public double Battery { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    /// <summary>调度 JSON 中 enable 有时为 bool，原 WinForms 用字符串 is_online。</summary>
    [JsonIgnore]
    public string? OnlineAsString => Enable ? "true" : "false";

    [JsonPropertyName("currentPosition")]
    public int CurrentPosition { get; set; }

    [JsonPropertyName("procState")]
    public string? ProcState { get; set; }

    /// <summary>调度字段 actionState（原 MainFrm 映射为 move_state）。</summary>
    [JsonPropertyName("actionState")]
    public string? ActionState { get; set; }

    [JsonPropertyName("movementState")]
    public string? MovementState { get; set; }

    /// <summary>优先 actionState，否则 movementState。</summary>
    [JsonIgnore]
    public string? EffectiveMoveState => ActionState ?? MovementState;

    [JsonPropertyName("orderName")]
    public string? OrderName { get; set; }

    [JsonPropertyName("endStationName")]
    public string? EndStationName { get; set; }

    [JsonPropertyName("endStationNo")]
    public int EndStationNo { get; set; }

    [JsonPropertyName("startStationName")]
    public string? StartStationName { get; set; }

    [JsonPropertyName("startStationNo")]
    public int StartStationNo { get; set; }

    [JsonPropertyName("emergencyState")]
    public string? EmergencyState { get; set; }

    [JsonPropertyName("batteryState")]
    public string? BatteryState { get; set; }

    [JsonPropertyName("loadState")]
    public int LoadState { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("position")]
    public PositionDto? Position { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
