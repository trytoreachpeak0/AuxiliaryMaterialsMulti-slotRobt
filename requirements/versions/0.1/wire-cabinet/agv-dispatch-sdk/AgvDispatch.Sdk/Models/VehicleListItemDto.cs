using System.Text.Json.Serialization;

namespace AgvDispatch.Sdk.Models;

/// <summary>多车列表项（原 ResultItem，在 VehicleInfoDto 基础上含扩展字段）。</summary>
public sealed class VehicleListItemDto : VehicleInfoDto
{
    [JsonPropertyName("breakSwitchState")]
    public string? BreakSwitchState { get; set; }

    [JsonPropertyName("currentMap")]
    public string? CurrentMap { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("enableTime")]
    public string? EnableTime { get; set; }

    [JsonPropertyName("existedInGroup")]
    public List<string>? ExistedInGroup { get; set; }

    [JsonPropertyName("hostPort")]
    public string? HostPort { get; set; }

    [JsonPropertyName("lockStatus")]
    public int LockStatus { get; set; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }

    [JsonPropertyName("nodeType")]
    public int NodeType { get; set; }

    [JsonPropertyName("powerMode")]
    public string? PowerMode { get; set; }

    [JsonPropertyName("productKey")]
    public string? ProductKey { get; set; }

    [JsonPropertyName("robotModel")]
    public string? RobotModel { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("taskType")]
    public string? TaskType { get; set; }

    [JsonPropertyName("totalFinishOrder")]
    public int TotalFinishOrder { get; set; }

    [JsonPropertyName("totalProcessOrder")]
    public int TotalProcessOrder { get; set; }
}
