using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WireCabinet.Hmi.Services;
using WireCabinet.Slots;

namespace WireCabinet.Hmi.Views;

public partial class MaintView : UserControl
{
    private const string MaintOpenWireNotice =
        "此操作仅打开门/锁，不会清除格口内的焊丝状态。\n" +
        "若格口内有焊丝：请勿取出；若已取出，请务必放回。";

    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private long? _selectedSlotId;
    private string? _selectedSlotNo;
    private bool _warnedEmptyDb;

    public MaintView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _pollTimer.Tick += (_, _) => RefreshPanel(reloadFromDb: true);
        SlotGrid.SideChanged += (_, _) => RefreshPanel(reloadFromDb: true);
        SlotGrid.SlotSelected += OnSlotSelected;
        SlotGrid.SelectionCleared += (_, _) => ClearSlotSelection();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Bootstrap.FlowSlots.Changed += OnSlotsChanged;
        App.SlotHardwarePoll.Updated += OnHardwareUpdated;
        App.IoHealth.Updated += OnIoHealthUpdated;
        App.DoorOps.BusyChanged += OnDoorOpsBusyChanged;
        if (!App.SlotHardwarePoll.IsRunning)
            App.SlotHardwarePoll.Start();
        _pollTimer.Start();
        UpdateConnectionSummary();
        ClearSlotSelection();
        RefreshPanel(reloadFromDb: true);
        ApplyDoorButtonStates();
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated += OnAgvPollUpdated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Bootstrap.FlowSlots.Changed -= OnSlotsChanged;
        App.SlotHardwarePoll.Updated -= OnHardwareUpdated;
        App.IoHealth.Updated -= OnIoHealthUpdated;
        App.DoorOps.BusyChanged -= OnDoorOpsBusyChanged;
        _pollTimer.Stop();
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated -= OnAgvPollUpdated;
    }

    private void OnAgvPollUpdated() =>
        Dispatcher.Invoke(ApplyDoorButtonStates);

    private void OnDoorOpsBusyChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(ApplyDoorButtonStates);

    private void OnSlotsChanged(object? sender, EventArgs e) => RefreshPanel(reloadFromDb: false);

    private void OnHardwareUpdated(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() => RefreshPanel(reloadFromDb: false));

    private void OnIoHealthUpdated() =>
        Dispatcher.Invoke(UpdateConnectionSummary);

    private void RefreshPanel(bool reloadFromDb = true)
    {
        UpdateConnectionSummary();

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
            SlotGrid.BindTiles([], enableSelection: true);
            SlotGrid.SetRefreshTime(DateTime.Now);
            ApplySummaryCounts([]);
            ClearSlotSelection();
            return;
        }

        _warnedEmptyDb = false;
        var sideSlots = CabinetSlotGridController.GetSideSlots(all, SlotGrid.ShowFront);
        var visuals = CabinetSlotGridController.BuildMaintVisuals(
            sideSlots,
            App.Bootstrap.SlotIo,
            App.SlotHardwarePoll.Snapshots);
        SlotGrid.BindTiles(visuals, enableSelection: true);
        SlotGrid.SetRefreshTime(DateTime.Now);
        ApplySummaryCounts(all);

        if (_selectedSlotId is > 0 &&
            all.Any(s => s.SlotId == _selectedSlotId.Value))
        {
            UpdateDetailForSlot(_selectedSlotId.Value, _selectedSlotNo);
        }
        else
        {
            ClearSlotSelection();
        }
    }

    private void UpdateConnectionSummary()
    {
        HwConfiguredText.Text = App.Bootstrap.Hardware.IsConfigured ? "Modbus 已配置" : "未配置（仅显示接线表）";
        IoModuleStatusPresenter.RenderDetailed(IoModuleListPanel, App.IoHealth.Statuses);
    }

    private void OnSlotSelected(object? sender, CabinetSlotSelectedEventArgs e)
    {
        _selectedSlotId = e.SlotId;
        _selectedSlotNo = e.SlotNo;
        if (e.SlotId is > 0)
            UpdateDetailForSlot(e.SlotId.Value, e.SlotNo);
    }

    private void ClearSlotSelection()
    {
        _selectedSlotId = null;
        _selectedSlotNo = null;
        DetailHintText.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
        DetailActionPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateDetailForSlot(long slotId, string? slotNo)
    {
        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == slotId);
        if (slot is null)
        {
            ClearSlotSelection();
            return;
        }

        DetailHintText.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DetailActionPanel.Visibility = Visibility.Visible;

        var map = App.Bootstrap.SlotIo.FindMapping(slot.SlotNo);
        var wired = map?.Wired == true;
        App.SlotHardwarePoll.Snapshots.TryGetValue(slot.SlotNo, out var hw);

        DetailSlotNoText.Text = slot.SlotNo;
        DetailPositionText.Text = SlotPositionFormatter.Format(map?.DoorPosition);
        DetailModuleText.Text = map?.ModuleKey ?? "—";
        DetailIoText.Text = MaintSlotTileModel.FormatIoLabel(map);
        DetailLockText.Text = hw?.LockClosed switch
        {
            true => "锁 DI：闭合（门已关）",
            false => "锁 DI：释放（门已开）",
            _ when hw?.ReadOk == false => "读数失败",
            _ => App.Bootstrap.Hardware.IsConfigured ? "未知" : "硬件未联调"
        };
        DetailEnabledText.Text = slot.IsEnabled ? "已启用" : "已禁用";
        DetailReadTimeText.Text = hw?.ReadOk == true
            ? $"硬件读数：{hw.ReadAt.ToLocalTime():HH:mm:ss}"
            : "硬件读数：—";

        BtnToggleDisable.Visibility = wired ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleDisable.Content = slot.IsEnabled ? "禁用此格" : "启用此格";
        ApplyDoorButtonStates();
    }

    private async void BtnOpenAllMaint_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmMaintOpen("确认打开所有格口", "将打开所有已接线格口（含已禁用格）。"))
            return;

        await RunMaintActionAsync((Button)sender, () => App.MhDoors.OpenAllMaintTrialAsync());
    }

    private async void BtnOpenFrontMaint_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmMaintOpen("确认打开前柜所有格口", "将打开前柜所有已接线格口（含已禁用格）。"))
            return;

        await RunMaintActionAsync((Button)sender, () => App.MhDoors.OpenFrontMaintTrialAsync());
    }

    private async void BtnOpenRearMaint_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmMaintOpen("确认打开后柜所有格口", "将打开后柜所有已接线格口（含已禁用格）。"))
            return;

        await RunMaintActionAsync((Button)sender, () => App.MhDoors.OpenRearMaintTrialAsync());
    }

    private async void BtnTrialOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmMaintOpen("确认试开格口", $"将试开格口 {_selectedSlotNo}。"))
            return;

        if (DetailPanel.Visibility == Visibility.Visible)
            DetailLockText.Text = "指令已下发，等待锁反馈…";

        await RunMaintActionAsync((Button)sender, () => App.MhDoors.OpenSingleMaintTrialAsync(_selectedSlotNo!), isTrial: true);
    }

    private async void BtnToggleDisable_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSlotId is not > 0)
            return;

        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == _selectedSlotId.Value);
        if (slot is null)
            return;

        var enabling = !slot.IsEnabled;
        var action = enabling ? "启用" : "禁用";
        var message = enabling
            ? $"确认启用格口 {_selectedSlotNo}？\n启用后该格口可用于存料。"
            : $"确认禁用格口 {_selectedSlotNo}？\n禁用后不可用于存料，但格内已有焊丝仍可取出。";
        if (MessageBox.Show(message, $"确认{action}格口", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
            MessageBoxResult.Yes)
            return;

        var trigger = (Button)sender;
        var originalContent = trigger.Content?.ToString() ?? "";
        trigger.Content = enabling ? "启用中…" : "禁用中…";
        trigger.IsEnabled = false;

        try
        {
            var (ok, resultMessage) = await App.Bootstrap.SlotControl.SetSlotEnabledAsync(
                _selectedSlotId.Value, enabling);
            if (!ok)
            {
                SetStatus(resultMessage);
                return;
            }

            App.Bootstrap.FlowSlots.Reload();
            SetStatus(resultMessage);
            RefreshPanel(reloadFromDb: false);
        }
        catch (Exception ex)
        {
            SetStatus($"操作失败：{ex.Message}");
        }
        finally
        {
            trigger.Content = originalContent;
            ApplyDoorButtonStates();
        }
    }

    private static bool ConfirmMaintOpen(string title, string actionLine)
    {
        var message = $"{actionLine}\n\n{MaintOpenWireNotice}";
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
               MessageBoxResult.Yes;
    }

    private async Task RunMaintActionAsync(
        Button trigger,
        Func<Task<(bool Ok, string Message)>> action,
        bool isTrial = false)
    {
        var originalContent = trigger.Content?.ToString() ?? "";
        trigger.Content = "开锁中…";

        try
        {
            var (ok, message) = await action();

            if (ok)
            {
                if (!App.Bootstrap.Hardware.IsConfigured && isTrial)
                    message += "（已写库 open；硬件未联调，格口不会按 DI 变色）";

                SetStatus(message);
                await App.SlotHardwarePoll.PollAsync();
                RefreshPanel(reloadFromDb: true);
            }
            else
            {
                SetStatus(message);
                if (message.Contains("当前正在", StringComparison.Ordinal))
                    MessageBox.Show(message, "请稍候", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"操作失败：{ex.Message}");
        }
        finally
        {
            trigger.Content = originalContent;
            ApplyDoorButtonStates();
        }
    }

    private void ApplyDoorButtonStates()
    {
        var doorOpsEnabled = !App.DoorOps.IsBusy && !App.UiGate.BlocksSlotDoorControls;
        BtnOpenAllMaint.IsEnabled = doorOpsEnabled;
        BtnOpenFrontMaint.IsEnabled = doorOpsEnabled;
        BtnOpenRearMaint.IsEnabled = doorOpsEnabled;
        if (DetailActionPanel.Visibility == Visibility.Visible)
        {
            BtnTrialOpen.IsEnabled = doorOpsEnabled;
            if (BtnToggleDisable.Visibility == Visibility.Visible)
                BtnToggleDisable.IsEnabled = true;
        }
    }

    private void SetStatus(string message)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.SetStatus(message);
    }

    private void ApplySummaryCounts(IReadOnlyList<SlotDoorState> all)
    {
        var counts = SlotSummaryHelper.ComputeMaint(all);
        SlotGrid.SetSummaryCounts(counts.Total, counts.Enabled, counts.Disabled);
    }
}
