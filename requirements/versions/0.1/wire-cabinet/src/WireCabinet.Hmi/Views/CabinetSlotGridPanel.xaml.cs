using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WireCabinet.Hmi.Views;

public sealed class CabinetSlotSelectedEventArgs : EventArgs
{
    public long? SlotId { get; init; }
    public string? SlotNo { get; init; }
    public CabinetSlotTileVisual? Tile { get; init; }
}

public partial class CabinetSlotGridPanel : UserControl
{
    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register(nameof(PanelTitle), typeof(string), typeof(CabinetSlotGridPanel),
            new PropertyMetadata("发货柜格口状态", OnPanelTitleChanged));

    public static readonly DependencyProperty EnableClearSelectionProperty =
        DependencyProperty.Register(nameof(EnableClearSelection), typeof(bool), typeof(CabinetSlotGridPanel),
            new PropertyMetadata(false, OnEnableClearSelectionChanged));

    private static void OnEnableClearSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CabinetSlotGridPanel panel)
            panel.UpdateClearSelectionHitTargets();
    }

    private static void OnPanelTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CabinetSlotGridPanel panel && e.NewValue is string t && panel.TitleText != null)
            panel.TitleText.Text = t;
    }

    public string PanelTitle
    {
        get => (string)GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }

    public bool EnableClearSelection
    {
        get => (bool)GetValue(EnableClearSelectionProperty);
        set => SetValue(EnableClearSelectionProperty, value);
    }

    public event EventHandler<CabinetSlotSelectedEventArgs>? SlotSelected;
    public event EventHandler? SelectionCleared;
    public event EventHandler? SideChanged;

    public bool ShowFront { get; private set; } = true;

    public CabinetSlotGridPanel()
    {
        InitializeComponent();
        TitleText.Text = PanelTitle;
        UpdateClearSelectionHitTargets();
    }

    private void UpdateClearSelectionHitTargets()
    {
        var captureBlanks = EnableClearSelection ? Brushes.Transparent : null;
        PanelRootGrid.Background = captureBlanks;
        SlotGridArea.Background = captureBlanks;
    }

    public void SetShowFront(bool front)
    {
        ShowFront = front;
        UpdateSideButtonStyles();
    }

    public void SetRefreshTime(DateTime time) =>
        LastRefreshText.Text = $"刷新时间：{time:HH:mm:ss}";

    public void BindTiles(IReadOnlyList<CabinetSlotTileVisual> tiles, bool enableSelection = false)
    {
        SlotGridHost.Children.Clear();
        foreach (var tile in tiles)
            SlotGridHost.Children.Add(CreateSlotBorder(tile, enableSelection));
    }

    private void BtnFrontSide_Click(object sender, RoutedEventArgs e)
    {
        if (ShowFront) return;
        ShowFront = true;
        UpdateSideButtonStyles();
        SideChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BtnRearSide_Click(object sender, RoutedEventArgs e)
    {
        if (!ShowFront) return;
        ShowFront = false;
        UpdateSideButtonStyles();
        SideChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSideButtonStyles()
    {
        BtnFrontSide.Style = (Style)FindResource(ShowFront ? "PrimaryButton" : "SecondaryButton");
        BtnRearSide.Style = (Style)FindResource(ShowFront ? "SecondaryButton" : "PrimaryButton");
    }

    private void PanelRootGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!EnableClearSelection) return;
        if (IsClickOnSideButton(e.OriginalSource as DependencyObject)) return;
        if (TryGetValidSlotTile(e.OriginalSource as DependencyObject) is not null) return;
        RaiseSelectionCleared();
        e.Handled = true;
    }

    private static bool IsClickOnSideButton(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Button { Name: "BtnFrontSide" or "BtnRearSide" })
                return true;
        }
        return false;
    }

    private Border CreateSlotBorder(CabinetSlotTileVisual tile, bool enableSelection)
    {
        var border = new Border
        {
            Margin = new Thickness(4),
            Padding = new Thickness(4, 3, 4, 3),
            ClipToBounds = true,
            Background = (Brush)FindResource(tile.BackgroundKey),
            BorderBrush = (Brush)FindResource(tile.BorderKey),
            BorderThickness = new Thickness(tile.BorderThickness),
            Tag = tile
        };

        if (enableSelection)
        {
            if (tile.SlotId is > 0)
            {
                border.Cursor = Cursors.Hand;
                border.MouseLeftButtonUp += OnValidSlotClicked;
            }
            else if (EnableClearSelection)
            {
                border.Cursor = Cursors.Hand;
                border.MouseLeftButtonUp += OnPlaceholderClicked;
            }
        }

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (tile.ShowMiddleRow)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var noText = new TextBlock
        {
            Text = tile.DisplayNo,
            Style = (Style)FindResource("SlotNoText"),
            Foreground = (Brush)FindResource(tile.NoForegroundKey),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(noText, 0);
        grid.Children.Add(noText);

        var statusRow = 1;
        if (tile.ShowMiddleRow)
        {
            var midText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(tile.MiddleText) ? "—" : tile.MiddleText,
                Style = (Style)FindResource("SlotTypeText"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 120
            };
            Grid.SetRow(midText, 1);
            grid.Children.Add(midText);
            statusRow = 2;
        }

        var statusText = new TextBlock
        {
            Text = tile.StatusText,
            Style = (Style)FindResource("SlotStatusText"),
            Foreground = (Brush)FindResource(tile.StatusForegroundKey),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (tile.StatusBold)
            statusText.FontWeight = FontWeights.Bold;
        Grid.SetRow(statusText, statusRow);
        grid.Children.Add(statusText);

        border.Child = grid;
        return border;
    }

    private void OnValidSlotClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: CabinetSlotTileVisual tile } || tile.SlotId is not > 0) return;
        e.Handled = true;
        SlotSelected?.Invoke(this, new CabinetSlotSelectedEventArgs
        {
            SlotId = tile.SlotId,
            SlotNo = tile.SlotNo,
            Tile = tile
        });
    }

    private void OnPlaceholderClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        RaiseSelectionCleared();
    }

    private void RaiseSelectionCleared() => SelectionCleared?.Invoke(this, EventArgs.Empty);

    private static CabinetSlotTileVisual? TryGetValidSlotTile(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Border { Tag: CabinetSlotTileVisual tile } && tile.SlotId is > 0)
                return tile;
        }
        return null;
    }
}
