using System.IO;
using WireFlowLab.Infrastructure;

namespace WireFlowLab.Flows;

/// <summary>从 flow-sql-map/index.yaml 加载 6 个流程的 flow.yaml。</summary>
public static class FlowLoader
{
    public static List<FlowDefinition> LoadAll()
    {
        var flows = new List<FlowDefinition>();
        var indexNode = Yaml.Parse(File.ReadAllText(LabPaths.FlowMapIndex));
        var list = Yaml.AsList(Yaml.Get(indexNode, "flows_index"));
        if (list is null) return flows;

        var baseDir = Path.GetDirectoryName(LabPaths.FlowMapIndex)!;
        foreach (var entry in list)
        {
            var rel = Yaml.Str(entry, "path");
            if (string.IsNullOrWhiteSpace(rel)) continue;
            var full = Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                flows.Add(LoadFlow(full));
        }
        return flows;
    }

    public static FlowDefinition LoadFlow(string path)
    {
        var root = Yaml.Parse(File.ReadAllText(path));

        var def = new FlowDefinition
        {
            FlowId = Yaml.Str(root, "flow_id") ?? "",
            Title = Yaml.Str(root, "title") ?? "",
            Diagram = Yaml.Str(root, "diagram") ?? "",
            Start = Yaml.Str(root, "start") ?? ""
        };

        var ctx = Yaml.AsMap(Yaml.Get(root, "context_defaults"));
        if (ctx is not null)
            foreach (var (k, v) in ctx)
                def.ContextDefaults[k.ToString()!] = v?.ToString() ?? "";

        var order = Yaml.AsList(Yaml.Get(root, "node_order"));
        if (order is not null)
            foreach (var o in order)
                def.NodeOrder.Add(o.ToString()!);

        var nodes = Yaml.AsMap(Yaml.Get(root, "nodes"));
        if (nodes is not null)
            foreach (var (key, val) in nodes)
            {
                var node = ParseNode(key.ToString()!, val);
                def.Nodes[node.Id] = node;
            }

        return def;
    }

    private static FlowNode ParseNode(string id, object? node)
    {
        var rawType = Yaml.Str(node, "type") ?? "";
        var n = new FlowNode
        {
            Id = id,
            Text = Yaml.Str(node, "text") ?? "",
            RawType = rawType,
            Type = MapType(rawType),
            DataSource = Yaml.Str(node, "data_source"),
            SqlId = Yaml.Str(node, "sql_id"),
            OpenSqlId = Yaml.Str(node, "open_sql_id"),
            Batch = Yaml.Str(node, "batch"),
            SlotRef = Yaml.Str(node, "slot_ref"),
            TerminalKind = Yaml.Str(node, "terminal_kind"),
            Note = Yaml.Str(node, "note")
        };

        var inputs = Yaml.AsList(Yaml.Get(node, "inputs"));
        if (inputs is not null)
            foreach (var im in inputs)
                n.Inputs.Add(new InputMapping
                {
                    Name = Yaml.Str(im, "name") ?? "",
                    Source = ValueSource.Parse(Yaml.Str(im, "from") ?? "")
                });

        var outputs = Yaml.AsList(Yaml.Get(node, "outputs"));
        if (outputs is not null)
            foreach (var of in outputs)
            {
                var fromStr = Yaml.Str(of, "from");
                n.Outputs.Add(new OutputField
                {
                    Name = Yaml.Str(of, "name") ?? "",
                    Label = Yaml.Str(of, "label") ?? "",
                    Default = Yaml.Str(of, "default") ?? "",
                    Numeric = string.Equals(Yaml.Str(of, "numeric"), "true", StringComparison.OrdinalIgnoreCase),
                    From = string.IsNullOrWhiteSpace(fromStr) ? null : ValueSource.Parse(fromStr)
                });
            }

        var check = Yaml.Get(node, "check");
        if (check is not null)
        {
            var refsList = Yaml.AsList(Yaml.Get(check, "refs"));
            var dc = new DecisionCheck
            {
                Kind = Yaml.Str(check, "kind") ?? "",
                Ref = Yaml.Str(check, "ref"),
                Node = Yaml.Str(check, "node"),
                Value = double.TryParse(Yaml.Str(check, "value"), out var d) ? d : 0,
                CompareValue = Yaml.Str(check, "value")
            };
            if (refsList is not null)
                foreach (var r in refsList) dc.Refs.Add(r.ToString()!);
            n.Check = dc;
        }

        var routing = Yaml.AsMap(Yaml.Get(node, "routing"));
        if (routing is not null)
            foreach (var (rk, rv) in routing)
            {
                // 路由值可能是字符串（next 节点）或嵌套 map（含 next 字段）
                var target = rv is Dictionary<object, object> m && m.TryGetValue("next", out var nx)
                    ? nx?.ToString()
                    : rv?.ToString();
                if (!string.IsNullOrWhiteSpace(target))
                    n.Routing[rk.ToString()!] = target!;
            }

        return n;
    }

    private static NodeType MapType(string t) => t switch
    {
        "user_input" => NodeType.UserInput,
        "query" => NodeType.Query,
        "decision" => NodeType.Decision,
        "write" => NodeType.Write,
        "slot_open" => NodeType.SlotOpen,
        "slot_open_batch" => NodeType.SlotOpenBatch,
        "slot_close" => NodeType.SlotClose,
        "terminal" => NodeType.Terminal,
        _ => NodeType.Unknown
    };
}
