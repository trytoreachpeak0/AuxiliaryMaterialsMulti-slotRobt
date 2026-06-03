using YamlDotNet.Serialization;

namespace WireFlowLab.Infrastructure;

/// <summary>YamlDotNet 反序列化辅助：把 YAML 读成嵌套的 dictionary/list/scalar 并提供安全访问。</summary>
public static class Yaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    public static object? Parse(string text) => Deserializer.Deserialize<object?>(text);

    public static Dictionary<object, object>? AsMap(object? node) => node as Dictionary<object, object>;

    public static List<object>? AsList(object? node) => node as List<object>;

    public static object? Get(object? node, string key)
    {
        if (node is Dictionary<object, object> map && map.TryGetValue(key, out var v))
            return v;
        return null;
    }

    public static string? Str(object? node, string key) => Get(node, key)?.ToString();

    public static string? AsStr(object? node) => node?.ToString();
}
