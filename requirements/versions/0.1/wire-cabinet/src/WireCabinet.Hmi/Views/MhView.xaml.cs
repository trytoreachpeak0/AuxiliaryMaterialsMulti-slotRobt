using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WireCabinet.Flows;
using WireCabinet.Hmi.Services;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Views;

public partial class MhView : UserControl
{
    private enum DepositPhase { Idle, WaitingClose }

    private const string BtnLabelIdle = "输入焊丝";
    private const string BtnLabelValidating = "校验中…";
    private const string BtnLabelWaitingClose = "请将焊丝放入格口";
    private const string BtnLabelOpenDoorBlocked = "检测有格口未关闭";

    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _warnedEmptyDb;
    private bool _depositInProgress;
    private DepositPhase _depositPhase = DepositPhase.Idle;
    private string? _allocatedSlotNo;
    private long _allocatedSlotId;
    private bool _allocatedSlotDoorWasOpen;
    private long? _selectedSlotId;
    private string? _selectedSlotNo;
    private bool? _lastMhPageAllowed;
    private MhOperationLock.BlockState _operationBlock = new(false, "", []);

    public MhView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _pollTimer.Tick += (_, _) => RefreshSlotPanel(reloadFromDb: true);
        SlotGrid.SideChanged += (_, _) => RefreshSlotPanel(reloadFromDb: true);
        SlotGrid.SlotSelected += OnSlotSelected;
        SlotGrid.SelectionCleared += (_, _) => ClearSlotSelection();
        ResetDepositUi();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Bootstrap.FlowSlots.Changed += OnSlotsChanged;
        App.SlotHardwarePoll.Updated += OnHardwareUpdated;
        App.DoorOps.BusyChanged += OnDoorOpsBusyChanged;
        App.MhDoors.SessionAutoEnded += OnDoorSessionAutoEnded;
        App.MhDoors.OperationLockChanged += OnOperationLockChanged;
        if (!App.SlotHardwarePoll.IsRunning)
            App.SlotHardwarePoll.Start();
        _pollTimer.Start();
        ClearSlotSelection();
        RefreshSlotPanel(reloadFromDb: true);
        ApplyPageAccess();
        FocusStationPrimaryInput();
        _lastMhPageAllowed = App.UiGate.CanUseMhPage;
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated += OnAgvPollUpdated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Bootstrap.FlowSlots.Changed -= OnSlotsChanged;
        App.SlotHardwarePoll.Updated -= OnHardwareUpdated;
        App.DoorOps.BusyChanged -= OnDoorOpsBusyChanged;
        App.MhDoors.SessionAutoEnded -= OnDoorSessionAutoEnded;
        App.MhDoors.OperationLockChanged -= OnOperationLockChanged;
        _pollTimer.Stop();
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated -= OnAgvPollUpdated;
    }

    private void SetStatus(string message)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.SetStatus(message);
    }

    private void ShowFlowError(string message, bool selectAll = false)
    {
        SetStatus(message);
        if (Window.GetWindow(this) is Window owner)
            FlowErrorDialog.Show(owner, message);
        FocusWireLotBoxDeferred(selectAll);
    }

    private void OnAgvPollUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            var allowed = App.UiGate.CanUseMhPage;
            if (_lastMhPageAllowed != allowed)
            {
                _lastMhPageAllowed = allowed;
                if (allowed)
                    FocusStationPrimaryInput();
            }

            ApplyPageAccess();
        });
    }

    private void OnDoorSessionAutoEnded(object? sender, string message) =>
        Dispatcher.Invoke(() =>
        {
            SetStatus(message);
            SyncOperationBlockState(reloadDb: true);
            ApplyPageAccess();
        });

    private void OnOperationLockChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            SyncOperationBlockState(reloadDb: true);
            ApplyPageAccess();
        });

    private void OnDoorOpsBusyChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            if (!App.DoorOps.IsBusy)
                SyncOperationBlockState(reloadDb: true);
            ApplyPageAccess();
        });

    private void OnHardwareUpdated(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            SyncOperationBlockState(reloadDb: false);
            RefreshSlotPanel(reloadFromDb: false);
            if (_depositPhase == DepositPhase.WaitingClose)
                TrackAllocatedDoorOpenState();
            _ = TryCompleteLoadWireAfterDoorClosedAsync();
        });

    private void OnSlotsChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() => RefreshSlotPanel(reloadFromDb: false));

    private void WireLotBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_depositPhase == DepositPhase.WaitingClose)
            return;
        ResetDepositUi(keepLotText: true);
    }

    private void WireLotBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        if (_depositPhase == DepositPhase.WaitingClose || _depositInProgress)
            return;
        if (!DepositActionBtn.IsEnabled)
            return;

        e.Handled = true;
        _ = RunDepositAsync();
    }

    private void ClearLotBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_depositPhase == DepositPhase.WaitingClose)
        {
            SetStatus("请关闭格口后完成存料，或等待写库完成。");
            return;
        }

        ResetDepositUi(refocusInput: true);
        SetStatus("已清空批号，请重新输入。");
    }

    private void ResetDepositUi(bool keepLotText = false, bool refocusInput = false, bool selectAll = false)
    {
        if (!keepLotText)
            WireLotBox.Clear();

        _depositPhase = DepositPhase.Idle;
        _depositInProgress = false;
        _allocatedSlotNo = null;
        _allocatedSlotId = 0;
        _allocatedSlotDoorWasOpen = false;

        if (App.Flows.IsFlowActive)
            App.Flows.EndFlow();
        else
            App.Flows.ClearStaleLoadSession();

        WireErrorText.Visibility = Visibility.Collapsed;
        WireInfoPanel.Visibility = Visibility.Collapsed;
        WireTypeText.Text = "";
        AllocText.Text = "";
        ApplyDepositButtonLabel();
        ApplyPageAccess();

        if (refocusInput)
            FocusWireLotBoxDeferred(selectAll);
    }

    private void ApplyDepositButtonLabel()
    {
        DepositActionBtn.Content = _depositPhase switch
        {
            DepositPhase.WaitingClose => BtnLabelWaitingClose,
            _ when _depositInProgress => BtnLabelValidating,
            _ when IsBlockedByOpenDoorsForNewOperations() => BtnLabelOpenDoorBlocked,
            _ => BtnLabelIdle
        };
    }

    private void FocusWireLotBox(bool selectAll = false)
    {
        WireLotBox.Focus();
        if (selectAll && !string.IsNullOrEmpty(WireLotBox.Text))
            WireLotBox.SelectAll();
    }

    private void FocusWireLotBoxDeferred(bool selectAll = false) =>
        Dispatcher.BeginInvoke(() => FocusWireLotBox(selectAll), DispatcherPriority.Input);

    public void FocusStationPrimaryInput()
    {
        if (!App.UiGate.CanUseMhPage)
            return;
        if (_depositPhase != DepositPhase.Idle)
            return;
        FocusWireLotBoxDeferred(selectAll: false);
    }

    private static bool ShouldSelectAllOnDepositFailure(string message) =>
        message.Contains("不存在", StringComparison.Ordinal)
        || message.Contains("已在柜内", StringComparison.Ordinal);

    private static string FormatAllocSlotDisplay(string slotNo)
    {
        if (string.IsNullOrWhiteSpace(slotNo))
            return "—";
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return $"前柜 {slotNo}（空闲）";
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return $"后柜 {slotNo}（空闲）";
        return $"{slotNo}（空闲）";
    }

    private async void DepositActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_depositInProgress)
            return;

        if (_depositPhase == DepositPhase.WaitingClose)
        {
            SetStatus("请关闭格口后系统将自动完成存料。");
            return;
        }

        if (App.DoorOps.IsBusy)
        {
            MessageBox.Show($"当前正在「{App.DoorOps.ActiveLabel}」，请稍候。", "请稍候",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunDepositAsync();
    }

    private async Task RunDepositAsync()
    {
        var lot = WireLotBox.Text.Trim();
        if (string.IsNullOrEmpty(lot))
        {
            ShowFlowError("请输入焊丝批号。");
            return;
        }

        SyncOperationBlockState(reloadDb: true);
        if (IsBlockedByOpenDoorsForNewOperations())
        {
            ShowFlowError(_operationBlock.Message);
            return;
        }

        var (allowed, gateMsg) = await MhStationAccess.CanRunLoadWireFlowAsync();
        if (!allowed)
        {
            ShowFlowError(gateMsg);
            return;
        }

        _depositInProgress = true;
        DepositActionBtn.IsEnabled = false;
        WireLotBox.IsEnabled = false;
        ApplyDepositButtonLabel();
        try
        {
            var (ok, message, reason) = await Task.Run(() =>
                App.Flows.AdvanceMhLoadWireValidateAndOpen(lot));

            if (!ok)
            {
                WireErrorText.Text = message.Contains("已在柜内", StringComparison.Ordinal)
                    ? message
                    : message.Contains("不存在", StringComparison.Ordinal)
                        ? "焊丝批号不存在"
                        : message;
                WireErrorText.Visibility = Visibility.Visible;
                WireInfoPanel.Visibility = Visibility.Collapsed;
                App.Flows.EndFlow();
                ShowFlowError(message, ShouldSelectAllOnDepositFailure(message));
                return;
            }

            if (reason != FlowPauseReason.AwaitingDoorClose)
            {
                App.Flows.EndFlow();
                ShowFlowError(message);
                return;
            }

            var spec = App.Flows.GetField("queryWireSpecByLotNo.spec")?.ToString() ?? "—";
            var slotNo = App.Flows.GetField("checkAvailableSlot.slot_no")?.ToString() ?? "";
            var slotIdObj = App.Flows.GetField("checkAvailableSlot.available_slot_id");
            _allocatedSlotId = slotIdObj switch
            {
                long l => l,
                int i => i,
                string s when long.TryParse(s, out var x) => x,
                _ => 0
            };
            _allocatedSlotNo = slotNo;
            _depositPhase = DepositPhase.WaitingClose;
            _allocatedSlotDoorWasOpen = false;

            WireErrorText.Visibility = Visibility.Collapsed;
            WireTypeText.Text = spec;
            AllocText.Text = FormatAllocSlotDisplay(slotNo);
            WireInfoPanel.Visibility = Visibility.Visible;
            var status = string.IsNullOrEmpty(gateMsg) ? message : $"{gateMsg} {message}";
            SetStatus(status);

            await App.SlotHardwarePoll.PollAsync();
            RefreshSlotPanel(reloadFromDb: true);
            TrackAllocatedDoorOpenState();
            await TryCompleteLoadWireAfterDoorClosedAsync();
        }
        catch (Exception ex)
        {
            App.Flows.EndFlow();
            ShowFlowError($"存料失败：{ex.Message}");
        }
        finally
        {
            _depositInProgress = false;
            WireLotBox.IsEnabled = App.UiGate.CanUseMhPage && _depositPhase != DepositPhase.WaitingClose;
            ApplyDepositButtonLabel();
            ApplyPageAccess();
        }
    }

    private async Task TryCompleteLoadWireAfterDoorClosedAsync()
    {
        if (_depositPhase != DepositPhase.WaitingClose || _allocatedSlotId <= 0)
            return;

        if (!App.Flows.IsFlowActive
            || !string.Equals(App.Flows.CurrentNodeId, "closeAvailableSlotDoor", StringComparison.OrdinalIgnoreCase))
            return;

        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == _allocatedSlotId);
        if (slot is null)
            return;

        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        App.SlotHardwarePoll.Snapshots.TryGetValue(slot.SlotNo, out var hw);
        var stillOpen = MhOperationLock.IsSlotBlockingOpen(slot, hw, hwConfigured);
        if (stillOpen)
        {
            _allocatedSlotDoorWasOpen = true;
            return;
        }

        if (!_allocatedSlotDoorWasOpen)
            return;

        if (_depositInProgress)
            return;

        _depositInProgress = true;
        try
        {
            var (ok, message, reason) = await Task.Run(() =>
                App.Flows.AdvanceMhLoadWireAfterDoorClosed());

            if (ok && reason == FlowPauseReason.Terminal)
            {
                App.Bootstrap.FlowSlots.Reload();
                RefreshSlotPanel(reloadFromDb: true);
                SetStatus("存料完成。");
                ResetDepositUi(refocusInput: true);
            }
            else if (!ok)
            {
                App.Flows.EndFlow();
                ResetDepositUi(keepLotText: true);
                ShowFlowError(message, ShouldSelectAllOnDepositFailure(message));
            }
        }
        catch (Exception ex)
        {
            ShowFlowError($"写库失败：{ex.Message}");
        }
        finally
        {
            _depositInProgress = false;
            ApplyDepositButtonLabel();
            ApplyPageAccess();
        }
    }

    private void TrackAllocatedDoorOpenState()
    {
        if (_allocatedSlotId <= 0)
            return;

        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == _allocatedSlotId);
        if (slot is null)
            return;

        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        App.SlotHardwarePoll.Snapshots.TryGetValue(slot.SlotNo, out var hw);
        if (MhOperationLock.IsSlotBlockingOpen(slot, hw, hwConfigured))
            _allocatedSlotDoorWasOpen = true;
    }

    private void SyncOperationBlockState(bool reloadDb = false) =>
        _operationBlock = MhOperationLock.Evaluate(reloadDb);

    private bool IsBlockedByOpenDoorsForNewOperations() =>
        _operationBlock.IsBlocked;

    private void ApplyPageAccess()
    {
        SyncOperationBlockState(reloadDb: false);
        ApplyDoorButtonStates();
        ApplyDepositButtonState();
        ApplyClearLotButtonState();
        if (_depositPhase != DepositPhase.WaitingClose)
            WireLotBox.IsEnabled = App.UiGate.CanUseMhPage && !IsBlockedByOpenDoorsForNewOperations();
    }

    private void UpdateOpenDoorWarning()
    {
        var wasBlocked = _operationBlock.IsBlocked;
        SyncOperationBlockState(reloadDb: false);

        if (_operationBlock.IsBlocked && _depositPhase != DepositPhase.WaitingClose)
        {
            if (!wasBlocked)
            {
                var status = _operationBlock.Message;
                if (MhInterruptedLoadStartupAlert.TryGetInterruptedSlotDoorOpen(out var record, out var doorOpen)
                    && record is not null && doorOpen)
                {
                    var hint = MhInterruptedLoadStartupAlert.BuildStatusHint(record, doorStillOpen: true);
                    if (!string.IsNullOrEmpty(hint))
                        status = hint!;
                }

                SetStatus(status);
            }
        }
        else if (_depositPhase == DepositPhase.Idle
                 && MhInterruptedLoadStartupAlert.TryGetInterruptedSlotDoorOpen(out var closedRecord, out var stillOpen)
                 && closedRecord is not null && !stillOpen)
        {
            var hint = MhInterruptedLoadStartupAlert.BuildStatusHint(closedRecord, doorStillOpen: false);
            if (!string.IsNullOrEmpty(hint))
                SetStatus(hint!);
        }

        ApplyDepositButtonLabel();
        ApplyPageAccess();
    }

    private void ApplyClearLotButtonState()
    {
        if (!App.UiGate.CanUseMhPage)
        {
            ClearLotBtn.IsEnabled = false;
            return;
        }

        if (_depositInProgress || App.DoorOps.IsBusy)
        {
            ClearLotBtn.IsEnabled = false;
            return;
        }

        if (IsBlockedByOpenDoorsForNewOperations() && _depositPhase == DepositPhase.Idle)
        {
            ClearLotBtn.IsEnabled = false;
            return;
        }

        ClearLotBtn.IsEnabled = _depositPhase == DepositPhase.Idle;
    }

    private void ApplyDepositButtonState()
    {
        if (!App.UiGate.CanUseMhPage)
        {
            DepositActionBtn.IsEnabled = false;
            return;
        }

        if (_depositInProgress)
        {
            DepositActionBtn.IsEnabled = false;
            return;
        }

        if (_depositPhase == DepositPhase.Idle && IsBlockedByOpenDoorsForNewOperations())
        {
            DepositActionBtn.IsEnabled = false;
            return;
        }

        var busy = App.DoorOps.IsBusy;
        DepositActionBtn.IsEnabled = _depositPhase switch
        {
            DepositPhase.Idle => !busy && !string.IsNullOrWhiteSpace(WireLotBox.Text),
            _ => false
        };
    }

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
            SlotGrid.BindTiles([], enableSelection: true);
            SlotGrid.SetRefreshTime(DateTime.Now);
            ApplySummaryCounts([], App.Bootstrap.Hardware.IsConfigured);
            ClearSlotSelection();
            return;
        }

        _warnedEmptyDb = false;
        var sideSlots = CabinetSlotGridController.GetSideSlots(all, SlotGrid.ShowFront);
        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        SlotGrid.BindTiles(CabinetSlotGridController.BuildMhVisuals(
            sideSlots,
            App.SlotHardwarePoll.Snapshots,
            hwConfigured), enableSelection: true);
        SlotGrid.SetRefreshTime(DateTime.Now);
        ApplySummaryCounts(all, hwConfigured);

        if (_selectedSlotId is > 0 && all.Any(s => s.SlotId == _selectedSlotId.Value))
            UpdateSlotInfoForSlot(_selectedSlotId.Value);
        else
            ClearSlotSelection();

        UpdateOpenDoorWarning();
    }

    private void ApplySummaryCounts(IReadOnlyList<SlotDoorState> all, bool hardwareConfigured)
    {
        var counts = SlotSummaryHelper.Compute(all, App.SlotHardwarePoll.Snapshots, hardwareConfigured);
        SlotGrid.SetSummaryCounts(counts.Total, counts.Idle, counts.Loaded, counts.Returned);
    }

    private void OnSlotSelected(object? sender, CabinetSlotSelectedEventArgs e)
    {
        _selectedSlotId = e.SlotId;
        _selectedSlotNo = e.SlotNo;
        if (e.SlotId is > 0)
            UpdateSlotInfoForSlot(e.SlotId.Value);
    }

    private void ClearSlotSelection()
    {
        _selectedSlotId = null;
        _selectedSlotNo = null;
        SlotInfoHintText.Text = "点击格口可显示焊丝信息";
        SlotInfoHintText.Visibility = Visibility.Visible;
        SlotInfoPanel.Visibility = Visibility.Collapsed;
        BtnOpenSelected.Visibility = Visibility.Collapsed;
    }

    private void UpdateSlotInfoForSlot(long slotId)
    {
        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == slotId);
        if (slot is null)
        {
            ClearSlotSelection();
            return;
        }

        _selectedSlotNo = slot.SlotNo;
        BtnOpenSelected.Visibility = Visibility.Visible;

        if (!SlotWireInfoFormatter.HasWire(slot))
        {
            SlotInfoHintText.Text = "该格口没有焊丝";
            SlotInfoHintText.Visibility = Visibility.Visible;
            SlotInfoPanel.Visibility = Visibility.Collapsed;
            return;
        }

        App.SlotHardwarePoll.Snapshots.TryGetValue(slot.SlotNo, out var hw);
        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;

        SlotInfoHintText.Visibility = Visibility.Collapsed;
        SlotInfoPanel.Visibility = Visibility.Visible;
        SlotInfoNoText.Text = SlotWireInfoFormatter.FormatDisplayNo(slot.SlotNo);
        SlotInfoLotText.Text = SlotWireInfoFormatter.FormatLotNo(slot);
        SlotInfoSpecText.Text = SlotWireInfoFormatter.FormatSpec(slot);
        SlotInfoStatusText.Text = SlotWireInfoFormatter.FormatWireStatus(slot, hw, hwConfigured);
        ApplyPageAccess();
    }

    private async void BtnOpenSelected_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedSlotNo))
            return;
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenSingleAsync(_selectedSlotNo));
    }

    private async void BtnOpenAvailable_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenAvailableWireSlotsAsync());

    private async void BtnOpenReturned_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenReturnedWireSlotsAsync());

    private async void BtnOpenAll_Click(object sender, RoutedEventArgs e) =>
        await RunDoorActionAsync((Button)sender, () => App.MhDoors.OpenAllSlotsAsync());

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

        if (App.Flows.IsFlowActive || _depositPhase == DepositPhase.WaitingClose)
        {
            MessageBox.Show("存料流程进行中，请先关闭格口并完成存料。", "请稍候",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SyncOperationBlockState(reloadDb: true);
        if (_operationBlock.IsBlocked)
        {
            SetStatus(_operationBlock.Message);
            MessageBox.Show(_operationBlock.Message, "请稍候", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var originalContent = trigger.Content?.ToString() ?? "";
        trigger.Content = "开锁中…";
        ApplyPageAccess();

        try
        {
            var (ok, message) = await action();
            SetStatus(message);
            if (!ok && message.Contains("当前正在", StringComparison.Ordinal))
                MessageBox.Show(message, "请稍候", MessageBoxButton.OK, MessageBoxImage.Information);
            if (ok)
            {
                SyncOperationBlockState(reloadDb: true);
                ApplyPageAccess();
                await App.SlotHardwarePoll.PollAsync();
                RefreshSlotPanel(reloadFromDb: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"开门失败：{ex.Message}");
        }
        finally
        {
            trigger.Content = originalContent;
            ApplyPageAccess();
        }
    }

    private void ApplyDoorButtonStates()
    {
        var depositBusy = App.Flows.IsFlowActive || _depositPhase == DepositPhase.WaitingClose;
        var blocked = IsBlockedByOpenDoorsForNewOperations();
        var enabled = App.UiGate.CanUseMhPage
                      && !App.DoorOps.IsBusy
                      && !depositBusy
                      && !blocked
                      && !App.UiGate.BlocksSlotDoorControls;
        BtnOpenAvailable.IsEnabled = enabled;
        BtnOpenReturned.IsEnabled = enabled;
        BtnOpenAll.IsEnabled = enabled;
        if (BtnOpenSelected.Visibility == Visibility.Visible)
            BtnOpenSelected.IsEnabled = enabled;
    }
}
