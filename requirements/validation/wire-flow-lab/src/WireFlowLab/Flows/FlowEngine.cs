using System.Globalization;
using System.Text;
using WireFlowLab.Data;

namespace WireFlowLab.Flows;

/// <summary>
/// YAML 驱动的流程引擎：按 node_order/outcome_routing 单步执行，解析 input_mappings → 绑定参数 →
/// 执行 SQL / 控门 / 判定 → 记录 trace，并在运行时检测流程/SQL 问题。
/// </summary>
public sealed class FlowEngine
{
    private readonly EngineServices _svc;

    public FlowEngine(EngineServices svc) => _svc = svc;

    public FlowDefinition? Flow { get; private set; }
    public FlowRunContext? Context { get; private set; }
    public string? CurrentNodeId { get; private set; }
    public bool Finished { get; private set; }
    public List<TraceEntry> Trace { get; } = new();

    private int _step;

    public FlowNode? CurrentNode => Flow is not null && CurrentNodeId is not null ? Flow.Node(CurrentNodeId) : null;

    public void Begin(FlowDefinition flow)
    {
        Flow = flow;
        Context = new FlowRunContext(flow);
        CurrentNodeId = flow.Start;
        Finished = false;
        _step = 0;
        Trace.Clear();
        _svc.Slot.Reload();
    }

    /// <summary>执行当前节点并前进一步，返回该步 trace。</summary>
    public TraceEntry StepOnce()
    {
        if (Flow is null || Context is null || CurrentNodeId is null || Finished)
            throw new InvalidOperationException("引擎未开始或已结束。");

        var node = Flow.Node(CurrentNodeId);
        _step++;

        if (node is null)
        {
            var missing = new TraceEntry
            {
                Step = _step,
                NodeId = CurrentNodeId,
                NodeText = "(节点不存在)",
                NodeType = "missing",
                Status = Severity.Error,
                Outcome = "error",
                Result = $"路由指向不存在的节点 '{CurrentNodeId}'"
            };
            missing.Findings.Add(new Finding
            {
                Severity = Severity.Error, FlowId = Flow.FlowId, NodeId = CurrentNodeId,
                Category = "断链", Message = $"outcome_routing 指向不存在的节点 '{CurrentNodeId}'。"
            });
            Trace.Add(missing);
            Finished = true;
            return missing;
        }

        var entry = Execute(node);
        Trace.Add(entry);

        if (node.Type == NodeType.Terminal || entry.NextNode is null)
            Finished = true;
        else
            CurrentNodeId = entry.NextNode;

        return entry;
    }

    private TraceEntry Execute(FlowNode node) => node.Type switch
    {
        NodeType.UserInput => ExecUserInput(node),
        NodeType.Query => ExecQuery(node),
        NodeType.Decision => ExecDecision(node),
        NodeType.Write => ExecWrite(node),
        NodeType.SlotOpen => ExecSlotOpen(node),
        NodeType.SlotOpenBatch => ExecSlotOpenBatch(node),
        NodeType.SlotClose => ExecSlotClose(node),
        NodeType.Terminal => ExecTerminal(node),
        _ => ExecUnknown(node)
    };

    // ---------- 各节点类型 ----------

    private TraceEntry ExecUserInput(FlowNode node)
    {
        var ctx = Context!;
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ctx.UserInputs.TryGetValue(node.Id, out var provided);
        var sb = new StringBuilder();
        foreach (var f in node.Outputs)
        {
            string val;
            if (f.From is not null)
                val = Resolve(f.From)?.ToString() ?? "";
            else if (provided is not null && provided.TryGetValue(f.Name, out var v))
                val = v;
            else
                val = f.Default;
            row[f.Name] = val;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{f.Name}={val}");
        }
        ctx.NodeResults[node.Id] = row;

        var (next, findings) = Route(node, "success");
        return Build(node, "success", next, "用户输入", sb.ToString(), findings);
    }

    private TraceEntry ExecQuery(FlowNode node)
    {
        var (parameters, display) = BuildParams(node);
        var item = node.SqlId is null ? null : _svc.Catalog.Find(node.SqlId);
        var findings = new List<Finding>();

        if (item is null)
        {
            findings.Add(Err(node, "缺少SQL", $"节点引用的 sql_id '{node.SqlId}' 在 sql-catalog 中不存在。"));
            var (nx2, f2) = Route(node, "error");
            findings.AddRange(f2);
            return Build(node, "error", nx2, node.SqlId ?? "", "(catalog 未找到)", findings, Severity.Error);
        }

        var db = string.Equals(node.DataSource, "mes", StringComparison.OrdinalIgnoreCase);
        var result = db ? _svc.Mes.Run(item, parameters) : _svc.AppDb.Run(item, parameters);

        var ctx = Context!;
        ctx.NodeResults[node.Id] = result.First ?? new(StringComparer.OrdinalIgnoreCase);
        ctx.RowCounts[node.Id] = result.Count;

        string key = result.HasError ? "error" : result.Count switch { 0 => "empty", 1 => "single", _ => "multiple" };
        if (result.HasError
            && string.Equals(node.Id, "queryWireQuotaCheck", StringComparison.OrdinalIgnoreCase)
            && IsQuotaReject(result.Error))
            key = "quota_reject";

        var (next, routeFindings) = Route(node, key);
        findings.AddRange(routeFindings);

        var resultText = result.HasError
            ? $"错误: {result.Error}"
            : $"{result.Count} 行" + (result.First is not null ? " | " + RowText(result.First) : "");

        var status = string.Equals(key, "quota_reject", StringComparison.OrdinalIgnoreCase)
            ? Severity.Warning
            : result.HasError ? Severity.Error : (routeFindings.Count > 0 ? Severity.Warning : Severity.Info);
        return Build(node, key, next, result.RenderedSql, resultText, findings, status, parameters);
    }

    private TraceEntry ExecDecision(FlowNode node)
    {
        var findings = new List<Finding>();
        var ctx = Context!;
        string sql = "";
        var prms = new Dictionary<string, object?>();

        if (node.SqlId is not null)
        {
            var item = _svc.Catalog.Find(node.SqlId);
            if (item is not null)
            {
                (prms, _) = BuildParams(node);
                var db = string.Equals(node.DataSource, "mes", StringComparison.OrdinalIgnoreCase);
                var r = db ? _svc.Mes.Run(item, prms) : _svc.AppDb.Run(item, prms);
                ctx.NodeResults[node.Id] = r.First ?? new(StringComparer.OrdinalIgnoreCase);
                sql = r.RenderedSql;
            }
        }

        var (passed, detail) = Evaluate(node.Check, node, findings);
        var key = passed ? "yes" : "no";
        var (next, routeFindings) = Route(node, key);
        findings.AddRange(routeFindings);
        return Build(node, key, next, sql, $"判定[{node.Check?.Kind}] => {(passed ? "是" : "否")} ({detail})", findings,
            findings.Count > 0 ? Severity.Warning : Severity.Info, prms);
    }

    private TraceEntry ExecWrite(FlowNode node)
    {
        var findings = new List<Finding>();

        if (string.Equals(node.Batch, "open_slots", StringComparison.OrdinalIgnoreCase))
        {
            var item0 = node.SqlId is null ? null : _svc.Catalog.Find(node.SqlId);
            var ids = _svc.Slot.OpenDoorIds();
            var (baseParams, _) = BuildParams(node);
            var errors = 0;
            foreach (var id in ids)
            {
                if (item0 is null) break;
                var p = new Dictionary<string, object?>(baseParams) { ["slot_id"] = id };
                var rr = _svc.AppDb.Run(item0, p);
                if (rr.HasError) errors++;
            }
            var (nx, rf) = Route(node, errors == 0 ? "success" : "error");
            findings.AddRange(rf);
            return Build(node, errors == 0 ? "success" : "error", nx,
                item0?.Sql ?? "", $"批量更新 {ids.Count} 个开门格口" + (errors > 0 ? $"，{errors} 个失败" : ""),
                findings, errors == 0 ? Severity.Info : Severity.Error);
        }

        var (parameters, display) = BuildParams(node);
        var item = node.SqlId is null ? null : _svc.Catalog.Find(node.SqlId);
        if (item is null)
        {
            findings.Add(Err(node, "缺少SQL", $"节点引用的 sql_id '{node.SqlId}' 在 sql-catalog 中不存在。"));
            var (nx2, f2) = Route(node, "error");
            findings.AddRange(f2);
            return Build(node, "error", nx2, node.SqlId ?? "", "(catalog 未找到)", findings, Severity.Error);
        }

        var isMes = string.Equals(node.DataSource, "mes", StringComparison.OrdinalIgnoreCase);
        var result = isMes ? _svc.Mes.Run(item, parameters) : _svc.AppDb.Run(item, parameters);

        Context!.NodeResults[node.Id] = result.First ?? new(StringComparer.OrdinalIgnoreCase);

        var key = result.HasError ? "error" : "success";
        var (next, routeFindings) = Route(node, key);
        findings.AddRange(routeFindings);

        var resultText = result.HasError
            ? $"错误: {result.Error}"
            : (result.First is not null ? RowText(result.First) : $"影响 {result.RowsAffected} 行");

        return Build(node, key, next, result.RenderedSql, resultText, findings,
            result.HasError ? Severity.Error : Severity.Info, parameters);
    }

    private TraceEntry ExecSlotOpen(FlowNode node)
    {
        var findings = new List<Finding>();
        var (parameters, _) = BuildParams(node);
        long slotId = ToLong(parameters.GetValueOrDefault("slot_id"));

        string sql = "";
        var ok = slotId > 0;
        if (ok)
        {
            _svc.Slot.OpenDoor(slotId);
            if (node.SqlId is not null && _svc.Catalog.Find(node.SqlId) is { } item)
            {
                var r = _svc.AppDb.Run(item, new Dictionary<string, object?> { ["slot_id"] = slotId });
                sql = r.RenderedSql;
                if (r.HasError) { ok = false; findings.Add(Err(node, "开门SQL", r.Error!)); }
            }
            Context!.NodeResults[node.Id] = new(StringComparer.OrdinalIgnoreCase) { ["opened_slot_id"] = slotId };
        }
        else
        {
            findings.Add(Err(node, "开门", "未解析出有效的格口 id。"));
        }

        var key = ok ? "success" : "error";
        var (next, rf) = Route(node, key);
        findings.AddRange(rf);
        return Build(node, key, next, sql, ok ? $"已打开格口 {slotId}" : "开门失败", findings,
            ok ? Severity.Info : Severity.Error, parameters);
    }

    private TraceEntry ExecSlotOpenBatch(FlowNode node)
    {
        var findings = new List<Finding>();
        var (parameters, _) = BuildParams(node);
        var listItem = node.SqlId is null ? null : _svc.Catalog.Find(node.SqlId);
        var openItem = node.OpenSqlId is null ? null : _svc.Catalog.Find(node.OpenSqlId);

        if (listItem is null)
        {
            findings.Add(Err(node, "缺少SQL", $"批量列举 sql_id '{node.SqlId}' 未找到。"));
            var (nx, f) = Route(node, "error");
            findings.AddRange(f);
            return Build(node, "error", nx, "", "(catalog 未找到)", findings, Severity.Error);
        }

        var list = _svc.AppDb.Run(listItem, parameters);
        var ids = list.Rows.Select(r => ToLong(r.GetValueOrDefault("slot_id"))).Where(x => x > 0).ToList();
        foreach (var id in ids)
        {
            _svc.Slot.OpenDoor(id);
            if (openItem is not null)
                _svc.AppDb.Run(openItem, new Dictionary<string, object?> { ["slot_id"] = id });
        }

        if (ids.Count == 0)
            findings.Add(Info(node, "空集合", "过滤条件未匹配到任何格口，直接进入关门判定。"));

        var (next, rf) = Route(node, "success");
        findings.AddRange(rf);
        return Build(node, "success", next, list.RenderedSql,
            $"打开 {ids.Count} 个格口: [{string.Join(", ", ids)}]", findings, Severity.Info, parameters);
    }

    private TraceEntry ExecSlotClose(FlowNode node)
    {
        var findings = new List<Finding>();
        var closed = _svc.Slot.CloseNextOpenDoor();
        if (closed is not null)
            Context!.Runtime["last_closed_slot_id"] = closed.Value.ToString(CultureInfo.InvariantCulture);

        var (next, rf) = Route(node, "success");
        findings.AddRange(rf);
        var text = closed is not null ? $"关闭格口 {closed}" : "当前无打开的门（空操作）";
        return Build(node, "success", next, "", text, findings);
    }

    private TraceEntry ExecTerminal(FlowNode node)
    {
        var kind = node.TerminalKind ?? "end";
        var sev = kind == "fail" ? Severity.Warning : Severity.Info;
        return Build(node, kind, null, "", $"流程结束 ({kind})", new(), sev);
    }

    private TraceEntry ExecUnknown(FlowNode node)
    {
        var findings = new List<Finding> { Err(node, "未知节点类型", $"无法识别的节点类型 '{node.RawType}'。") };
        return Build(node, "error", null, "", "未知节点类型", findings, Severity.Error);
    }

    // ---------- 判定 ----------

    private (bool passed, string detail) Evaluate(DecisionCheck? check, FlowNode node, List<Finding> findings)
    {
        if (check is null)
        {
            findings.Add(Warn(node, "判定缺失", "decision 节点缺少 check 定义，默认走 yes。"));
            return (true, "无 check");
        }

        switch (check.Kind)
        {
            case "exists":
            {
                var count = check.Node is not null && Context!.RowCounts.TryGetValue(check.Node, out var c) ? c : 0;
                return (count > 0, $"{check.Node} 行数={count}");
            }
            case "field_not_empty":
            {
                var v = Context!.GetRef(check.Ref ?? "");
                var ok = !IsEmpty(v);
                return (ok, $"{check.Ref}={Display(v)}");
            }
            case "field_not_empty_and_not_equals":
            {
                var v = Context!.GetRef(check.Ref ?? "");
                if (IsEmpty(v))
                    return (false, $"{check.Ref}=(空)");
                var s = v!.ToString() ?? "";
                var reject = string.Equals(s, check.CompareValue ?? "", StringComparison.Ordinal);
                return (!reject, $"{check.Ref}={Display(v)} != {check.CompareValue}");
            }
            case "all_fields_not_empty":
            {
                var ok = check.Refs.All(r => !IsEmpty(Context!.GetRef(r)));
                var detail = string.Join(", ", check.Refs.Select(r => $"{r}={Display(Context!.GetRef(r))}"));
                return (ok, detail);
            }
            case "numeric_le":
            {
                var v = Context!.GetRef(check.Ref ?? "");
                var num = ToDouble(v);
                return (num <= check.Value, $"{check.Ref}={Display(v)} <= {check.Value}");
            }
            case "submit_success":
            {
                var v = Context!.GetRef(check.Ref ?? "")?.ToString();
                var ok = IsSubmitSuccess(v);
                return (ok, $"{check.Ref}='{v}'");
            }
            case "all_doors_closed":
            {
                object? v = Context!.NodeResults.TryGetValue(node.Id, out var row) && row.TryGetValue("all_closed", out var av) ? av : null;
                bool ok = v is not null ? ToLong(v) == 1 : _svc.Slot.AreAllDoorsClosed();
                return (ok, $"all_closed={Display(v)}");
            }
            default:
                findings.Add(Warn(node, "未知判定", $"未知 check.kind '{check.Kind}'，默认走 yes。"));
                return (true, check.Kind);
        }
    }

    private static bool IsSubmitSuccess(string? v) => MatTransResult.IsSuccess(v);

    private static bool IsQuotaReject(string? error) =>
        !string.IsNullOrWhiteSpace(error)
        && error.Contains("剩余产量不能大于待完工产量", StringComparison.OrdinalIgnoreCase);

    // ---------- 参数与取值 ----------

    private (Dictionary<string, object?>, string) BuildParams(FlowNode node)
    {
        var item = node.SqlId is null ? null : _svc.Catalog.Find(node.SqlId);
        var isMatTrans = item is not null && MatTransResult.IsMatTransItem(item);

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        foreach (var im in node.Inputs)
        {
            var val = Resolve(im.Source);
            if (isMatTrans && string.Equals(im.Name, "V_ShelfLife", StringComparison.OrdinalIgnoreCase))
                val = MesDateTimeFormat.ToOracleString(val);
            dict[im.Name] = val;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{im.Name}={Display(val)}");
        }
        return (dict, sb.ToString());
    }

    private object? Resolve(ValueSource src)
    {
        switch (src.SourceKind)
        {
            case ValueSource.Kind.Const:
                return src.Literal;
            case ValueSource.Kind.System:
                return src.Field switch
                {
                    "configured_agv_no" => _svc.SystemAgvNo,
                    "configured_mes_writer" => _svc.SystemMesWriter,
                    _ => ""
                };
            case ValueSource.Kind.Context:
                return Context!.Runtime.TryGetValue(src.Field ?? "", out var cv) ? cv : null;
            case ValueSource.Kind.Node:
                if (src.Field is null) return null;
                return Context!.NodeResults.TryGetValue(src.Node ?? "", out var row) && row.TryGetValue(src.Field, out var v) ? v : null;
            default:
                return null;
        }
    }

    // ---------- 路由 ----------

    private (string? next, List<Finding> findings) Route(FlowNode node, string key)
    {
        var findings = new List<Finding>();
        if (node.Routing.TryGetValue(key, out var next))
            return (next, findings);

        // 终止节点没有路由是正常的
        if (node.Type == NodeType.Terminal) return (null, findings);

        // 执行失败：不回落到 single/yes，由界面统一提示并结束本流程（见 flow-sql-map README outcome_routing）
        if (string.Equals(key, "error", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Err(node, "执行失败", "SQL/连接/catalog 异常，未配置流程图 error 出边；流程终止。"));
            return (null, findings);
        }

        findings.Add(Warn(node, "未路由", $"结果 '{key}' 未在 outcome_routing 中定义。"));

        // 回退：优先 single/success，否则取第一个
        foreach (var fk in new[] { "single", "success", "yes" })
            if (node.Routing.TryGetValue(fk, out var fb)) return (fb, findings);
        var first = node.Routing.Values.FirstOrDefault();
        return (first, findings);
    }

    // ---------- 工具 ----------

    private TraceEntry Build(FlowNode node, string outcome, string? next, string sql, string result,
        List<Finding> findings, Severity status = Severity.Info, IDictionary<string, object?>? prms = null)
    {
        return new TraceEntry
        {
            Step = _step,
            NodeId = node.Id,
            NodeText = node.Text,
            NodeType = node.RawType,
            DataSource = node.DataSource ?? "",
            SqlId = node.SqlId,
            RenderedSql = sql,
            Parameters = prms is null ? "" : string.Join(", ", prms.Select(p => $"{p.Key}={Display(p.Value)}")),
            Result = result,
            Outcome = outcome,
            NextNode = next,
            Status = status,
            Findings = findings
        };
    }

    private Finding Err(FlowNode n, string cat, string msg) => new() { Severity = Severity.Error, FlowId = Flow!.FlowId, NodeId = n.Id, Category = cat, Message = msg };
    private Finding Warn(FlowNode n, string cat, string msg) => new() { Severity = Severity.Warning, FlowId = Flow!.FlowId, NodeId = n.Id, Category = cat, Message = msg };
    private Finding Info(FlowNode n, string cat, string msg) => new() { Severity = Severity.Info, FlowId = Flow!.FlowId, NodeId = n.Id, Category = cat, Message = msg };

    private static bool IsEmpty(object? v) => v is null || string.IsNullOrWhiteSpace(v.ToString());
    private static string Display(object? v)
    {
        if (v is null or DBNull) return "NULL";
        if (v is string s && s.Length == 0) return "\"\"";
        return v.ToString() ?? "\"\"";
    }
    private static string RowText(Dictionary<string, object?> row) => string.Join(", ", row.Select(kv => $"{kv.Key}={Display(kv.Value)}"));

    private static long ToLong(object? v)
    {
        if (v is null) return 0;
        if (v is long l) return l;
        if (v is int i) return i;
        return long.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    private static double ToDouble(object? v)
    {
        if (v is null) return double.NaN;
        if (v is double d) return d;
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : double.NaN;
    }
}
