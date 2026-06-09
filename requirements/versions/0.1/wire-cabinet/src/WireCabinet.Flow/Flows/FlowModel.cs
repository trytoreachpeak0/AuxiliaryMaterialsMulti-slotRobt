namespace WireCabinet.Flows;

public enum NodeType { UserInput, Query, Decision, Write, SlotOpen, SlotOpenBatch, SlotClose, Terminal, Unknown }

public sealed class ValueSource
{
    public enum Kind { Node, Const, System, Context }
    public Kind SourceKind { get; init; }
    public string? Node { get; init; }
    public string? Field { get; init; }
    public string? Literal { get; init; }
    public string Raw { get; init; } = "";

    public static ValueSource Parse(string raw)
    {
        raw = raw?.Trim() ?? "";
        var idx = raw.IndexOf(':');
        if (idx < 0) return new ValueSource { SourceKind = Kind.Const, Literal = raw, Raw = raw };

        var prefix = raw[..idx];
        var rest = raw[(idx + 1)..];
        switch (prefix)
        {
            case "node":
                var dot = rest.IndexOf('.');
                return dot < 0
                    ? new ValueSource { SourceKind = Kind.Node, Node = rest, Raw = raw }
                    : new ValueSource { SourceKind = Kind.Node, Node = rest[..dot], Field = rest[(dot + 1)..], Raw = raw };
            case "system":
                return new ValueSource { SourceKind = Kind.System, Field = rest, Raw = raw };
            case "context":
                return new ValueSource { SourceKind = Kind.Context, Field = rest, Raw = raw };
            case "const":
                var lit = rest.Trim();
                if (lit.Length >= 2 && lit.StartsWith('"') && lit.EndsWith('"')) lit = lit[1..^1];
                return new ValueSource { SourceKind = Kind.Const, Literal = lit, Raw = raw };
            default:
                return new ValueSource { SourceKind = Kind.Const, Literal = raw, Raw = raw };
        }
    }
}

public sealed class InputMapping
{
    public string Name { get; init; } = "";
    public ValueSource Source { get; init; } = new();
}

public sealed class OutputField
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string Default { get; init; } = "";
    public bool Numeric { get; init; }
    public ValueSource? From { get; init; }
}

public sealed class DecisionCheck
{
    public string Kind { get; init; } = "";
    public string? Ref { get; init; }
    public List<string> Refs { get; init; } = new();
    public string? Node { get; init; }
    public double Value { get; init; }
    /// <summary>字符串比较用（如 field_not_empty_and_not_equals 的排除值）。</summary>
    public string? CompareValue { get; init; }
}

public sealed class FlowNode
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public NodeType Type { get; init; } = NodeType.Unknown;
    public string RawType { get; init; } = "";
    public string? DataSource { get; init; }
    public string? SqlId { get; init; }
    public string? OpenSqlId { get; init; }
    public string? Batch { get; init; }
    public string? SlotRef { get; init; }
    public string? TerminalKind { get; init; }
    public string? Note { get; init; }
    public List<InputMapping> Inputs { get; init; } = new();
    public List<OutputField> Outputs { get; init; } = new();
    public DecisionCheck? Check { get; set; }
    public Dictionary<string, string> Routing { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FlowDefinition
{
    public string FlowId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Diagram { get; init; } = "";
    public string Start { get; init; } = "";
    public Dictionary<string, string> ContextDefaults { get; init; } = new();
    public List<string> NodeOrder { get; init; } = new();
    public Dictionary<string, FlowNode> Nodes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public FlowNode? Node(string id) => Nodes.TryGetValue(id, out var n) ? n : null;
    public override string ToString() => Title;
}
