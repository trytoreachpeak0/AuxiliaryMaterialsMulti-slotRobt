using System.Text.Json.Serialization;

namespace AgvDispatch.Sdk.Models;

public sealed class CreateMoveOrderRequest
{
    [JsonPropertyName("mission")]
    public List<MissionItemDto> Mission { get; set; } = new();

    [JsonPropertyName("orderName")]
    public string OrderName { get; set; } = "";

    [JsonPropertyName("appointVehicleKey")]
    public string AppointVehicleKey { get; set; } = "";
}

public sealed class CancelOrderCommandRequest
{
    [JsonPropertyName("commandType")]
    public string CommandType { get; set; } = "CMD_ORDER_CANCEL";

    [JsonPropertyName("disableVehicle")]
    public bool DisableVehicle { get; set; }
}

public sealed class ServiceCommandBody
{
    [JsonPropertyName("messageId")]
    public long MessageId { get; set; }

    [JsonPropertyName("mqCallback")]
    public MqCallbackDto? MqCallback { get; set; }

    [JsonPropertyName("thingsProperties")]
    public object? ThingsProperties { get; set; }
}

public sealed class MqCallbackDto
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "string";

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = "string";
}

public sealed class CancelEmergencyRequest
{
    [JsonPropertyName("deviceCommandDto")]
    public DeviceCommandDto DeviceCommandDto { get; set; } = new();

    [JsonPropertyName("deviceKeys")]
    public List<string> DeviceKeys { get; set; } = new();

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = "cancelEmergency";
}

public sealed class DeviceCommandDto
{
    [JsonPropertyName("messageId")]
    public long MessageId { get; set; }
}

public sealed class CreateOrderResult
{
    public string? RawResponse { get; set; }
    public string? OrderId { get; set; }
}

public sealed class VehicleSnapshot
{
    public VehicleInfoDto Vehicle { get; set; } = new();
    public OrderDetailDto? Order { get; set; }
}
