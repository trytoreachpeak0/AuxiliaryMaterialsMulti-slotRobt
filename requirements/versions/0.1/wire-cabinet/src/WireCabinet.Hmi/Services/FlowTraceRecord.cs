using System.Text;
using WireCabinet.Flows;

namespace WireCabinet.Hmi.Services;

public sealed class FlowTraceRecord
{
    public DateTime Timestamp { get; init; }
    public string FlowId { get; init; } = "";
    public string FlowTitle { get; init; } = "";
    public string SessionId { get; init; } = "";
    public bool IsSessionMarker { get; init; }
    public int Step { get; init; }
    public string NodeId { get; init; } = "";
    public string NodeText { get; init; } = "";
    public string NodeType { get; init; } = "";
    public string DataSource { get; init; } = "";
    public string? SqlId { get; init; }
    public string Parameters { get; init; } = "";
    public string ParametersDetail { get; init; } = "";
    public string RenderedSql { get; init; } = "";
    public string Result { get; init; } = "";
    public string Outcome { get; init; } = "";
    public string? NextNode { get; init; }
    public Severity Status { get; init; } = Severity.Info;
    public List<Finding> Findings { get; init; } = new();

    public string TimeText => Timestamp.ToString("HH:mm:ss.fff");

    public string SessionShort => SessionId.Length > 8 ? SessionId[..8] : SessionId;

    public static FlowTraceRecord FromEntry(
        TraceEntry entry,
        string flowId,
        string flowTitle,
        string sessionId,
        DateTime timestamp)
    {
        return new FlowTraceRecord
        {
            Timestamp = timestamp,
            FlowId = flowId,
            FlowTitle = flowTitle,
            SessionId = sessionId,
            Step = entry.Step,
            NodeId = entry.NodeId,
            NodeText = entry.NodeText,
            NodeType = entry.NodeType,
            DataSource = entry.DataSource,
            SqlId = entry.SqlId,
            Parameters = entry.Parameters,
            ParametersDetail = entry.ParametersDetail,
            RenderedSql = entry.RenderedSql,
            Result = entry.Result,
            Outcome = entry.Outcome,
            NextNode = entry.NextNode,
            Status = entry.Status,
            Findings = entry.Findings.ToList()
        };
    }

    public static FlowTraceRecord SessionMarker(
        string flowId,
        string flowTitle,
        string sessionId,
        string message,
        DateTime timestamp)
    {
        return new FlowTraceRecord
        {
            Timestamp = timestamp,
            FlowId = flowId,
            FlowTitle = flowTitle,
            SessionId = sessionId,
            IsSessionMarker = true,
            NodeType = "session",
            NodeId = message,
            Result = message,
            Status = Severity.Info
        };
    }

    public string BuildDetailText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"时间: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"流程: {FlowTitle} ({FlowId})");
        sb.AppendLine($"会话: {SessionId}");
        if (IsSessionMarker)
        {
            sb.AppendLine($"标记: {Result}");
            return sb.ToString();
        }

        sb.AppendLine($"步骤 #{Step}");
        sb.AppendLine($"节点: {NodeId} ({NodeType})");
        if (!string.IsNullOrWhiteSpace(NodeText))
            sb.AppendLine($"说明: {NodeText}");
        if (!string.IsNullOrWhiteSpace(DataSource))
            sb.AppendLine($"数据源: {DataSource}");
        if (!string.IsNullOrWhiteSpace(SqlId))
            sb.AppendLine($"SqlId: {SqlId}");
        if (!string.IsNullOrWhiteSpace(Parameters) || !string.IsNullOrWhiteSpace(ParametersDetail))
        {
            sb.AppendLine("参数:");
            sb.AppendLine(!string.IsNullOrWhiteSpace(ParametersDetail) ? ParametersDetail : Parameters);
        }
        if (!string.IsNullOrWhiteSpace(RenderedSql))
        {
            sb.AppendLine("SQL:");
            sb.AppendLine(RenderedSql);
        }
        if (!string.IsNullOrWhiteSpace(Result))
        {
            sb.AppendLine("结果:");
            sb.AppendLine(Result);
        }
        sb.AppendLine($"Outcome: {Outcome}");
        if (!string.IsNullOrWhiteSpace(NextNode))
            sb.AppendLine($"下一步: {NextNode}");
        sb.AppendLine($"状态: {Status}");
        if (Findings.Count > 0)
        {
            sb.AppendLine("Findings:");
            foreach (var f in Findings)
                sb.AppendLine($"  {f}");
        }

        return sb.ToString();
    }

    public string BuildExportLine()
    {
        if (IsSessionMarker)
            return $"[{TimeText}] [{SessionShort}] --- {Result} ---";

        var sb = new StringBuilder();
        sb.Append($"[{TimeText}] [{SessionShort}] [{Step:D2}] {NodeId} ({NodeType}) => {Outcome}");
        if (!string.IsNullOrWhiteSpace(NextNode))
            sb.Append($" -> {NextNode}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(DataSource) || !string.IsNullOrWhiteSpace(SqlId))
            sb.AppendLine($"  数据源={DataSource} SqlId={SqlId}");
        if (!string.IsNullOrWhiteSpace(Parameters) || !string.IsNullOrWhiteSpace(ParametersDetail))
        {
            sb.AppendLine("  参数:");
            var paramText = !string.IsNullOrWhiteSpace(ParametersDetail) ? ParametersDetail : Parameters;
            foreach (var line in paramText.Split('\n'))
                sb.AppendLine($"    {line.TrimEnd('\r')}");
        }
        if (!string.IsNullOrWhiteSpace(RenderedSql))
            sb.AppendLine($"  SQL: {RenderedSql}");
        if (!string.IsNullOrWhiteSpace(Result))
            sb.AppendLine($"  结果: {Result}");
        foreach (var f in Findings)
            sb.AppendLine($"  {f}");
        return sb.ToString().TrimEnd();
    }
}
