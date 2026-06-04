using System.Text.Json.Serialization;

namespace AgvDispatch.Sdk.Models;

/// <summary>调度通用响应包装。</summary>
public sealed class DispatchApiResponse<T>
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("msgDetail")]
    public string? MsgDetail { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("tid")]
    public string? Tid { get; set; }
}
