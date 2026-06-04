using System.Text.Json.Serialization;

namespace AgvDispatch.Sdk.Models;

public sealed class PositionDto
{
    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }

    [JsonPropertyName("pitch")]
    public int Pitch { get; set; }

    [JsonPropertyName("roll")]
    public int Roll { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("yaw")]
    public int Yaw { get; set; }

    [JsonPropertyName("z")]
    public int Z { get; set; }
}
