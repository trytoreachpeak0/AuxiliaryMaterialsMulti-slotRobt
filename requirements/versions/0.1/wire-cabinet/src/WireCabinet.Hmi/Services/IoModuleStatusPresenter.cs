using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WireCabinet.Hmi.Services;

/// <summary>IO 模块连接状态列表 UI（顶栏紧凑 / MAINT 详情）。</summary>
public static class IoModuleStatusPresenter
{
    private static readonly SolidColorBrush OnlineBrush = new(Color.FromRgb(0x35, 0xD1, 0x7F));
    private static readonly SolidColorBrush OfflineBrush = new(Color.FromRgb(0xE7, 0x4C, 0x3C));
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromRgb(0x9A, 0xA0, 0xA6));

    public static void RenderCompact(StackPanel panel, IReadOnlyList<IoModuleStatus> statuses)
    {
        panel.Children.Clear();
        if (statuses.Count == 0)
        {
            panel.Children.Add(PlaceholderText("未配置", 12, UnknownBrush));
            return;
        }

        foreach (var s in statuses)
            panel.Children.Add(BuildRow(s, showStatusText: false, margin: new Thickness(6, 0, 0, 0), fontSize: 12));
    }

    public static void RenderDetailed(Panel panel, IReadOnlyList<IoModuleStatus> statuses)
    {
        panel.Children.Clear();
        if (statuses.Count == 0)
        {
            panel.Children.Add(PlaceholderText("未配置", 12, UnknownBrush));
            return;
        }

        var grid = new UniformGrid { Columns = 2 };
        foreach (var s in statuses)
        {
            grid.Children.Add(BuildRow(
                s,
                showStatusText: true,
                margin: new Thickness(0, 0, 4, 4),
                fontSize: 12));
        }

        panel.Children.Add(grid);
    }

    private static UIElement BuildRow(
        IoModuleStatus s,
        bool showStatusText,
        Thickness margin,
        double fontSize)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = margin,
            ToolTip = $"{s.Key}\n{s.Host}:{s.Port}\n{FormatConnectionStatus(s.Online)}"
        };
        row.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = StatusBrush(s.Online),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });
        row.Children.Add(new TextBlock
        {
            Text = showStatusText
                ? $"{s.ShortLabel}  {FormatConnectionStatus(s.Online)}"
                : s.ShortLabel,
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap
        });
        return row;
    }

    private static TextBlock PlaceholderText(string text, double fontSize, Brush foreground) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = foreground
        };

    public static string FormatConnectionStatus(bool? online) =>
        online switch
        {
            true => "已连接",
            false => "未连接",
            _ => "检测中…"
        };

    private static Brush StatusBrush(bool? online) =>
        online switch
        {
            true => OnlineBrush,
            false => OfflineBrush,
            _ => UnknownBrush
        };
}
