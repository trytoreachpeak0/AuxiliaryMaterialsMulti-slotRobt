using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WireCabinet.Hmi.Services;
using WireCabinet.Slots;

namespace WireCabinet.Hmi.Views;

public partial class MhView : UserControl
{
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _warnedEmptyDb;

    public MhView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _pollTimer.Tick += (_, _) => RefreshSlotPanel(reloadFromDb: true);
        SlotGrid.SideChanged += (_, _) => RefreshSlotPanel(reloadFromDb: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Bootstrap.FlowSlots.Changed += OnSlotsChanged;
        App.DoorOps.BusyChanged += OnDoorOpsBusyChanged;
        _pollTimer.Start();
        RefreshSlotPanel(reloadFromDb: true);
        ApplyDoorButtonStates();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Bootstrap.FlowSlots.Changed -= OnSlotsChanged;
        App.DoorOps.BusyChanged -= OnDoorOpsBusyChanged;
        _pollTimer.Stop();
    }

    private void OnDoorOpsBusyChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(ApplyDoorButtonStates);

    private void OnSlotsChanged(object? sender, EventArgs e) => RefreshSlotPanel(reloadFromDb: false);

    private void RefreshSlotPanel(bool reloadFromDb = true)
    {
        if (reloadFromDb)
        {
            try
            {
                App.Bootstrap.FlowSlots.Reload();
            }
            catch (Exception ex)
            {
                if (!_warnedEmptyDb)
                {
                    _warnedEmptyDb = true;
                    SetStatus($"格口数据加载失败：{ex.Message}");
                }
                return;
            }
        }

        var all = App.Bootstrap.FlowSlots.Slots;
        if (all.Count == 0)
        {
            if (!_warnedEmptyDb)
            {
                _warnedEmptyDb = true;
                SetStatus("格口表为空，请检查应用数据库是否已初始化。");
            }
            SlotGrid.BindTiles([]);
            SlotGrid.SetRefreshTime(DateTime.Now);
            UpdateSummaryCounts([]);
            return;
        }

        _warnedEmptyDb = false;
        var sideSlots = CabinetSlotGridController.GetSideSlots(all, SlotGrid.ShowFront);
        SlotGrid.BindTiles(CabinetSlotGridController.BuildMhVisuals(sideSlots));
        SlotGrid.SetRefreshTime(DateTime.Now);
        UpdateSummaryCounts(all);
    }

    private async void BtnOpenSingle_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenSingleAsync(SingleSlotBox.Text));

    private async void BtnOpenAvailable_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenAvailableWireSlotsAsync());

    private async void BtnOpenReturned_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenReturnedWireSlotsAsync());

    private async void BtnOpenAll_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenAllSlotsAsync());

    private void BtnEndDoorSession_Click(object sender, RoutedEventArgs e)
    {
        App.MhDoors.EndSession();
        SetStatus("已结束开门会话。");
    }

    private async Task RunDoorActionAsync(Button trigger, Func<Task<(bool Ok, string Message)>> action)
    {
        if (App.DoorOps.IsBusy)
        {
            MessageBox.Show(
                $"当前正在「{App.DoorOps.ActiveLabel}」，请等待完成。",
                "请稍候",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var originalContent = trigger.Content?.ToString() ?? "";
        trigger.Content = "开锁中…";
        ApplyDoorButtonStates();

        try
        {
            var (ok, message) = await action();
            SetStatus(message);
            if (!ok && message.Contains("当前正在", StringComparison.Ordinal))
                MessageBox.Show(message, "请稍候", MessageBoxButton.OK, MessageBoxImage.Information);
            if (ok)
                RefreshSlotPanel(reloadFromDb: true);
        }
        catch (Exception ex)
        {
            SetStatus($"开门失败：{ex.Message}");
        }
        finally
        {
            trigger.Content = originalContent;
            ApplyDoorButtonStates();
        }
    }

    private void ApplyDoorButtonStates()
    {
        var enabled = !App.DoorOps.IsBusy;
        BtnOpenAvailable.IsEnabled = enabled;
        BtnOpenReturned.IsEnabled = enabled;
        BtnOpenAll.IsEnabled = enabled;
        BtnOpenSingle.IsEnabled = enabled;
        SingleSlotBox.IsEnabled = enabled;
    }

    private void UpdateSummaryCounts(IReadOnlyList<SlotDoorState> all)
    {
        TotalCountText.Text = all.Count.ToString();
        IdleCountText.Text = all.Count(s => s.BizState == "idle" && !s.IsOpen).ToString();
        LoadedCountText.Text = all.Count(s => s.BizState == "available_wire").ToString();
        ReturnedCountText.Text = all.Count(s => s.BizState == "returned_wire").ToString();
    }

    private void SetStatus(string message)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.SetStatus(message);
    }
}
