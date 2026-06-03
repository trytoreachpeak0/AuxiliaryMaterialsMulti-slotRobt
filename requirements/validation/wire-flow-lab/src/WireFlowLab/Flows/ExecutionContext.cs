namespace WireFlowLab.Flows;

public enum Severity { Info, Warning, Error }

public sealed class Finding
{
    public Severity Severity { get; init; }
    public string FlowId { get; init; } = "";
    public string NodeId { get; init; } = "";
    public string Category { get; init; } = "";
    public string Message { get; init; } = "";

    public override string ToString() => $"[{Severity}] {FlowId}/{NodeId} ({Category}): {Message}";
}

public sealed class TraceEntry
{
    public int Step { get; init; }
    public string NodeId { get; init; } = "";
    public string NodeText { get; init; } = "";
    public string NodeType { get; init; } = "";
    public string DataSource { get; init; } = "";
    public string? SqlId { get; init; }
    public string RenderedSql { get; init; } = "";
    public string Parameters { get; init; } = "";
    public string Result { get; init; } = "";
    public string Outcome { get; init; } = "";
    public string? NextNode { get; init; }
    public Severity Status { get; init; } = Severity.Info;
    public List<Finding> Findings { get; init; } = new();
}

/// <summary>一次流程执行的运行时上下文：节点结果、行数、运行期上下文变量、用户输入。</summary>
public sealed class FlowRunContext
{
    public FlowDefinition Flow { get; }

    public Dictionary<string, Dictionary<string, object?>> NodeResults { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> RowCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Runtime { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>用户输入：nodeId -> (field -> value)。UI 或自测在执行 user_input 前填充。</summary>
    public Dictionary<string, Dictionary<string, string>> UserInputs { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FlowRunContext(FlowDefinition flow)
    {
        Flow = flow;
        foreach (var (k, v) in flow.ContextDefaults)
            Runtime[k] = v;
    }

    public object? GetRef(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var dot = reference.IndexOf('.');
        if (dot < 0) return null;
        var node = reference[..dot];
        var field = reference[(dot + 1)..];
        return NodeResults.TryGetValue(node, out var row) && row.TryGetValue(field, out var v) ? v : null;
    }
}
