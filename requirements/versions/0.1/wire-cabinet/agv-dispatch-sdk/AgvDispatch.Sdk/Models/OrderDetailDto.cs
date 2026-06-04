using System.Text.Json.Serialization;

namespace AgvDispatch.Sdk.Models;

public sealed class MissionItemDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("destination")]
    public int Destination { get; set; }

    [JsonPropertyName("mapId")]
    public int MapId { get; set; }

    [JsonPropertyName("map_id")]
    public int MapIdSnake { get; set; }

    [JsonPropertyName("actionName")]
    public string? ActionName { get; set; }

    [JsonPropertyName("actionId")]
    public int ActionId { get; set; }

    [JsonPropertyName("actionParam1")]
    public int ActionParam1 { get; set; }

    [JsonPropertyName("actionParam2")]
    public int ActionParam2 { get; set; }
}

/// <summary>订单详情（原 Root / VehicleOrderInfoStr）。</summary>
public sealed class OrderDetailDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("orderState")]
    public int OrderState { get; set; }

    [JsonPropertyName("order_state")]
    public int OrderStateSnake { get; set; }

    [JsonIgnore]
    public int EffectiveOrderState => OrderState != 0 ? OrderState : OrderStateSnake;

    [JsonPropertyName("upperId")]
    public string? UpperId { get; set; }

    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }

    [JsonPropertyName("doneTime")]
    public string? DoneTime { get; set; }

    [JsonPropertyName("startStationName")]
    public string? StartStationName { get; set; }

    [JsonPropertyName("endStationName")]
    public string? EndStationName { get; set; }

    [JsonPropertyName("mission")]
    public List<MissionItemDto>? Mission { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
