using System.Collections.ObjectModel;
using System.Windows.Threading;
using WireCabinet.Core.Config;
using WireCabinet.Flows;

namespace WireCabinet.Hmi.Services;

/// <summary>被动收集 FlowEngine Trace，供调试窗口展示。</summary>
public sealed class FlowTraceHub
{
    private readonly FlowTraceWindowOptions _options;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<FlowTraceRecord> Records { get; } = new();

    public event EventHandler? RecordAppended;

    public FlowTraceHub(FlowTraceWindowOptions options, Dispatcher dispatcher)
    {
        _options = options;
        _dispatcher = dispatcher;
    }

    public string BeginSession(string flowId, string flowTitle)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        Append(FlowTraceRecord.SessionMarker(
            flowId,
            flowTitle,
            sessionId,
            "会话开始",
            DateTime.Now));
        return sessionId;
    }

    public void EndSession(string flowId, string flowTitle, string sessionId)
    {
        Append(FlowTraceRecord.SessionMarker(
            flowId,
            flowTitle,
            sessionId,
            "会话结束",
            DateTime.Now));
    }

    public void PublishNewEntries(
        FlowEngine engine,
        string flowId,
        string flowTitle,
        string sessionId,
        ref int publishedCount)
    {
        var trace = engine.Trace;
        if (publishedCount >= trace.Count)
            return;

        var now = DateTime.Now;
        for (var i = publishedCount; i < trace.Count; i++)
        {
            Append(FlowTraceRecord.FromEntry(trace[i], flowId, flowTitle, sessionId, now));
        }

        publishedCount = trace.Count;
    }

    public void Clear() => RunOnUi(Records.Clear);

    public IReadOnlyList<FlowTraceRecord> SnapshotRecords(string? flowIdFilter)
    {
        if (string.IsNullOrEmpty(flowIdFilter))
            return Records.ToList();

        return Records
            .Where(r => string.Equals(r.FlowId, flowIdFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void Append(FlowTraceRecord record)
    {
        RunOnUi(() =>
        {
            Records.Add(record);
            TrimForFlow(record.FlowId);
            RecordAppended?.Invoke(this, EventArgs.Empty);
        });
    }

    private void TrimForFlow(string flowId)
    {
        var max = _options.MaxEntriesPerFlow;
        if (max <= 0)
            return;

        var count = Records.Count(r =>
            string.Equals(r.FlowId, flowId, StringComparison.OrdinalIgnoreCase));
        if (count <= max)
            return;

        var remove = count - max;
        for (var i = 0; i < Records.Count && remove > 0;)
        {
            if (string.Equals(Records[i].FlowId, flowId, StringComparison.OrdinalIgnoreCase))
            {
                Records.RemoveAt(i);
                remove--;
            }
            else
                i++;
        }
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Invoke(action);
    }
}
