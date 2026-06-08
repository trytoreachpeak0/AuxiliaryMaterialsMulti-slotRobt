using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using WireCabinet.Flows;
using WireCabinet.Hmi.Services;

namespace WireCabinet.Hmi.Views;

public partial class FlowTraceWindow : Window
{
    private readonly FlowTraceHub _hub;
    private readonly ICollectionView _view;
    private string? _flowFilter;

    private sealed record FlowFilterItem(string? FlowId, string Title)
    {
        public override string ToString() => Title;
    }

    public FlowTraceWindow(FlowTraceHub hub, IReadOnlyList<FlowDefinition> flows)
    {
        InitializeComponent();
        _hub = hub;

        FlowFilterCombo.Items.Add(new FlowFilterItem(null, "全部"));
        foreach (var flow in flows)
            FlowFilterCombo.Items.Add(new FlowFilterItem(flow.FlowId, flow.Title));

        _view = CollectionViewSource.GetDefaultView(_hub.Records);
        _view.Filter = FilterRecord;
        TraceListView.ItemsSource = _view;
        FlowFilterCombo.SelectedIndex = 0;

        _hub.RecordAppended += OnRecordAppended;
        Closed += (_, _) => _hub.RecordAppended -= OnRecordAppended;
    }

    private bool FilterRecord(object item)
    {
        if (item is not FlowTraceRecord record)
            return false;
        if (string.IsNullOrEmpty(_flowFilter))
            return true;
        return string.Equals(record.FlowId, _flowFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void FlowFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FlowFilterCombo.SelectedItem is not FlowFilterItem item)
            return;

        _flowFilter = item.FlowId;
        _view.Refresh();
    }

    private void OnRecordAppended(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _view.Refresh();
            if (AutoScrollCheck.IsChecked == true)
                ScrollToLatest();
        });
    }

    private void ScrollToLatest()
    {
        FlowTraceRecord? latest = null;
        foreach (FlowTraceRecord record in _view)
        {
            latest = record;
        }

        if (latest is null)
            return;

        TraceListView.ScrollIntoView(latest);
        TraceListView.SelectedItem = latest;
    }

    private void TraceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TraceListView.SelectedItem is FlowTraceRecord record)
            DetailTextBox.Text = record.BuildDetailText();
        else
            DetailTextBox.Text = "";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _hub.Clear();
        DetailTextBox.Text = "";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出流程 Trace",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"flow-trace-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("WireCabinet 流程调试 Trace 导出");
        sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrEmpty(_flowFilter))
            sb.AppendLine($"流程筛选: {_flowFilter}");
        sb.AppendLine(new string('-', 80));

        foreach (FlowTraceRecord record in _view)
        {
            sb.AppendLine(record.BuildExportLine());
            sb.AppendLine();
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show(this, $"已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
