using WireFlowLab.Data;
using WireFlowLab.Flows;

namespace WireFlowLab.Validation;

/// <summary>对流程定义做静态校验（不执行）：断链、缺 SQL、入参来源缺失、不可达、分支不全等。</summary>
public static class FlowValidator
{
    public static List<Finding> Validate(FlowDefinition flow, SqlCatalog catalog)
    {
        var findings = new List<Finding>();
        void Add(Severity s, string node, string cat, string msg) =>
            findings.Add(new Finding { Severity = s, FlowId = flow.FlowId, NodeId = node, Category = cat, Message = msg });

        if (!flow.Nodes.ContainsKey(flow.Start))
            Add(Severity.Error, flow.Start, "起点", $"start 指向的节点 '{flow.Start}' 不存在。");

        foreach (var id in flow.NodeOrder)
            if (!flow.Nodes.ContainsKey(id))
                Add(Severity.Error, id, "node_order", $"node_order 中的 '{id}' 在 nodes 中没有定义。");

        foreach (var (id, node) in flow.Nodes)
        {
            // 路由目标存在性
            foreach (var (key, target) in node.Routing)
                if (!flow.Nodes.ContainsKey(target))
                    Add(Severity.Error, id, "断链", $"分支 '{key}' 指向不存在的节点 '{target}'。");

            // 非终止节点必须有出边
            if (node.Type != NodeType.Terminal && node.Routing.Count == 0)
                Add(Severity.Error, id, "死路", "非终止节点没有任何 outcome_routing 出边。");

            // sql_id / open_sql_id 解析
            CheckSql(node.SqlId, id, "sql_id");
            CheckSql(node.OpenSqlId, id, "open_sql_id");

            // 需要 SQL 的节点是否缺 sql_id
            if ((node.Type == NodeType.Query || node.Type == NodeType.Write) && string.IsNullOrWhiteSpace(node.SqlId))
                Add(Severity.Error, id, "缺少SQL", $"{node.RawType} 节点缺少 sql_id。");

            // input_mappings 来源节点存在性 + 顺序（来源应在当前节点之前出现过）
            var orderIndex = flow.NodeOrder.IndexOf(id);
            foreach (var im in node.Inputs)
            {
                if (im.Source.SourceKind != ValueSource.Kind.Node) continue;
                var srcNode = im.Source.Node ?? "";
                if (!flow.Nodes.ContainsKey(srcNode))
                {
                    Add(Severity.Error, id, "入参来源", $"参数 '{im.Name}' 来源节点 '{srcNode}' 不存在。");
                    continue;
                }
                var srcIndex = flow.NodeOrder.IndexOf(srcNode);
                if (orderIndex >= 0 && srcIndex >= 0 && srcIndex > orderIndex)
                    Add(Severity.Warning, id, "入参顺序", $"参数 '{im.Name}' 来源节点 '{srcNode}' 在 node_order 中位于当前节点之后，运行期可能取不到值。");
            }

            // 查询节点分支完整性
            if (node.Type == NodeType.Query)
            {
                foreach (var k in new[] { "empty", "error" })
                    if (!node.Routing.ContainsKey(k))
                        Add(Severity.Warning, id, "分支不全", $"查询节点缺少 '{k}' 分支，运行期将回退路由。");
            }

            // 判定节点
            if (node.Type == NodeType.Decision)
            {
                if (node.Check is null)
                    Add(Severity.Warning, id, "判定缺失", "decision 节点缺少 check。");
                foreach (var k in new[] { "yes", "no" })
                    if (!node.Routing.ContainsKey(k))
                        Add(Severity.Warning, id, "分支不全", $"判定节点缺少 '{k}' 分支。");
            }
        }

        // 可达性
        var reachable = Reachable(flow);
        foreach (var (id, _) in flow.Nodes)
            if (!reachable.Contains(id))
                Add(Severity.Warning, id, "不可达", "从 start 出发无法到达该节点。");

        return findings;

        void CheckSql(string? sqlId, string nodeId, string field)
        {
            if (string.IsNullOrWhiteSpace(sqlId)) return;
            if (catalog.Find(sqlId) is null)
                Add(Severity.Error, nodeId, "缺少SQL", $"{field} '{sqlId}' 在 sql-catalog 中不存在。");
        }
    }

    private static HashSet<string> Reachable(FlowDefinition flow)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        if (flow.Nodes.ContainsKey(flow.Start)) queue.Enqueue(flow.Start);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            var node = flow.Node(id);
            if (node is null) continue;
            foreach (var target in node.Routing.Values)
                if (!visited.Contains(target)) queue.Enqueue(target);
        }
        return visited;
    }
}
