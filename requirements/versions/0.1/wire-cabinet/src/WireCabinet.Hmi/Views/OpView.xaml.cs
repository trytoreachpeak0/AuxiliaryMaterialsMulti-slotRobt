using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WireCabinet.Flows;
using WireCabinet.Hmi.Services;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Views;

public partial class OpView : UserControl
{
    private enum DoorWaitPhase { None, ReturnSlot, IssueSlot }

    private readonly OpStepFlow _steps = new();
    private bool _flowStarted;
    private bool? _lastOpPageAllowed;
    private bool _actionInProgress;
    private DoorWaitPhase _doorPhase = DoorWaitPhase.None;
    private long _returnSlotId;
    private long _issueSlotId;
    private bool _returnDoorWasOpen;
    private bool _issueDoorWasOpen;

    private static readonly string[] TeamOptions = ["甲班", "乙班", "丙班", "丁班"];
    private static readonly string[] ShiftOptions = ["早班", "中班", "夜班"];

    public OpView()
    {
        InitializeComponent();
        TeamBox.ItemsSource = TeamOptions;
        ShiftBox.ItemsSource = ShiftOptions;
        TeamBox.SelectedIndex = 0;
        ShiftBox.SelectedIndex = 0;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        QueryWireBtn.PreviewMouseDown += (_, _) => WireLotBox.Focus();
        ApplyStepGating();
        SetStatus("请填写操作员 ID、班组、班次，完成后点「校验」。");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.SlotHardwarePoll.Updated += OnHardwareUpdated;
        if (!App.SlotHardwarePoll.IsRunning)
            App.SlotHardwarePoll.Start();
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated += OnAgvPollUpdated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.SlotHardwarePoll.Updated -= OnHardwareUpdated;
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated -= OnAgvPollUpdated;
    }

    private void OnHardwareUpdated(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() => _ = TryCompleteDoorCloseAsync());

    private void OnAgvPollUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            var allowed = App.UiGate.CanUseOpPage;
            if (_lastOpPageAllowed != allowed)
            {
                _lastOpPageAllowed = allowed;
                var reason = App.UiGate.GetStationBlockReason("OP");
                if (!string.IsNullOrEmpty(reason))
                    SetStatus(reason);
            }

            ApplyStepGating();
        });
    }

    private void SetStatus(string message)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.SetStatus(message);
    }

    private void ApplyStepGating()
    {
        SetStepOpacity(Step0Panel, 0);
        SetStepOpacity(Step1Panel, 1);
        SetStepOpacity(Step2Panel, 2);
        SetStepOpacity(Step3Panel, 3);
        SetStepOpacity(Step4Panel, 4);
        SetStepOpacity(Step5Panel, 5);

        var atStation = App.UiGate.CanUseOpPage;
        var busy = _actionInProgress;

        OpIdBox.IsEnabled = atStation && _steps.IsStepActive(0) && !busy;
        TeamBox.IsEnabled = atStation && _steps.IsStepActive(0) && !busy;
        ShiftBox.IsEnabled = atStation && _steps.IsStepActive(0) && !busy;
        ValidateOpBtn.IsEnabled = atStation && _steps.IsStepActive(0)
            && !string.IsNullOrWhiteSpace(OpIdBox.Text) && !busy;

        WireLotBox.IsEnabled = atStation && _steps.IsStepActive(1) && !busy;
        QueryWireBtn.IsEnabled = atStation && _steps.IsStepActive(1)
            && !string.IsNullOrWhiteSpace(WireLotBox.Text) && !busy;

        EqpNoBox.IsEnabled = atStation && _steps.IsStepActive(2) && !busy;
        ValidateEqpBtn.IsEnabled = atStation && _steps.IsStepActive(2)
            && !string.IsNullOrWhiteSpace(EqpNoBox.Text) && !busy;

        RemainingQtyBox.IsEnabled = atStation && _steps.IsStepActive(3) && !busy;
        ConfirmQuotaBtn.IsEnabled = atStation && _steps.IsStepActive(3)
            && !string.IsNullOrWhiteSpace(RemainingQtyBox.Text) && !busy;

        SubmitReturnBtn.IsEnabled = atStation
            && (_steps.IsStepActive(4) || _doorPhase == DoorWaitPhase.ReturnSlot) && !busy;
        SubmitIssueBtn.IsEnabled = atStation
            && (_steps.IsStepActive(5) || _doorPhase == DoorWaitPhase.IssueSlot) && !busy;
        RestartBtn.IsEnabled = atStation && _doorPhase == DoorWaitPhase.None && !busy;
    }

    private void SetStepOpacity(FrameworkElement panel, int stepIndex) =>
        panel.Opacity = _steps.IsStepFuture(stepIndex) ? 0.45 : 1.0;

    private void RestartBtn_Click(object sender, RoutedEventArgs e)
    {
        _flowStarted = false;
        _doorPhase = DoorWaitPhase.None;
        App.Flows.EndFlow();
        _steps.ResetAll();
        OpIdBox.Clear();
        TeamBox.SelectedIndex = 0;
        ShiftBox.SelectedIndex = 0;
        OpNameText.Text = "—";
        WireLotBox.Clear();
        ClearWireResults();
        EqpNoBox.Clear();
        ClearEqpResults();
        RemainingQtyBox.Clear();
        ClearQuotaResults();
        ReturnSlotText.Text = "—";
        IssueSlotText.Text = "—";
        IssueSlotHintText.Text = "—";
        ReturnSlotHintText.Text = "—";
        ApplyStepGating();
        SetStatus("已重新开始：请填写操作员信息并点「校验」。");
    }

    private void OpField_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyStepGating();

    private async void ValidateOpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OpIdBox.Text))
            return;

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法校验操作员。");
            return;
        }

        var (allowed, gateMsg) = await OpStationAccess.CanRunOpFlowAsync();
        if (!allowed)
        {
            SetStatus(gateMsg);
            return;
        }

        const string flowId = OpStationAccess.FlowId;
        if (!_flowStarted)
        {
            if (!App.Flows.TryBeginFlow(flowId, out var beginMsg))
            {
                SetStatus(beginMsg);
                return;
            }
            _flowStarted = true;
        }

        await RunFlowActionAsync(async () =>
        {
            var team = TeamBox.SelectedItem?.ToString() ?? "";
            var shift = ShiftBox.SelectedItem?.ToString() ?? "";
            App.Flows.FillOpStep0(OpIdBox.Text.Trim(), team, shift);
            var (ok, msg, reason) = await Task.Run(() => App.Flows.AdvanceOpOperatorValidate());
            if (!ok)
            {
                _flowStarted = false;
                return (false, msg);
            }

            if (reason != FlowPauseReason.UserInput)
            {
                _flowStarted = false;
                return (false, msg.Length > 0 ? msg : "操作员校验未通过。");
            }

            var name = App.Flows.GetField("queryOPById.operator_name")?.ToString();
            OpNameText.Text = string.IsNullOrWhiteSpace(name) ? "—" : name;
            _steps.CompleteStep(0);
            var status = gateMsg.Length > 0
                ? $"{gateMsg} 操作员校验通过。请扫描或输入归还焊丝批号，点「查询」。"
                : "操作员校验通过。请扫描或输入归还焊丝批号，点「查询」。";
            return (true, status);
        });
    }

    private void WireLotBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_doorPhase != DoorWaitPhase.None)
            return;
        if (string.IsNullOrWhiteSpace(WireLotBox.Text))
            ResetFromStep(1);
        ApplyStepGating();
    }

    private async void QueryWireBtn_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        var lot = WireLotBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(lot))
        {
            SetStatus("请输入焊丝批号。");
            return;
        }

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法查询焊丝。");
            return;
        }

        if (!App.Flows.IsFlowActive)
        {
            SetStatus("请先完成操作员校验。");
            return;
        }

        await RunFlowActionAsync(async () =>
        {
            SetStatus("正在查询焊丝批号…");
            var (ok, msg, reason) = await Task.Run(() => App.Flows.AdvanceOpWireLotQuery(lot));
            if (!ok)
            {
                ClearWireResults();
                if (!App.Flows.IsFlowActive)
                    _flowStarted = false;
                return (false, msg);
            }

            if (reason != FlowPauseReason.AwaitingAction)
            {
                if (!App.Flows.IsFlowActive)
                    _flowStarted = false;
                return (false, msg);
            }

            BindWireQueryResults();
            _steps.CompleteStep(1);
            return (true, "焊丝批号查询通过。请输入机台号并点「校验」。");
        });
    }

    private void EqpNoBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_doorPhase != DoorWaitPhase.None)
            return;
        if (string.IsNullOrWhiteSpace(EqpNoBox.Text))
            ResetFromStep(2);
        ApplyStepGating();
    }

    private async void ValidateEqpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EqpNoBox.Text))
            return;

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法校验机台。");
            return;
        }

        if (!App.Flows.IsFlowActive)
        {
            SetStatus("请先完成焊丝批号查询。");
            return;
        }

        var eqpNo = EqpNoBox.Text.Trim();
        await RunFlowActionAsync(async () =>
        {
            var (ok, msg, reason) = await Task.Run(() => App.Flows.AdvanceOpEqpValidate(eqpNo));
            if (!ok)
            {
                ClearEqpResults();
                return (false, msg);
            }

            if (reason != FlowPauseReason.AwaitingAction)
                return (false, msg);

            BindEqpQueryResults();
            _steps.CompleteStep(2);
            return (true, "机台校验通过。请输入剩余待焊芯片数并点「用量校验」。");
        });
    }

    private void RemainingQtyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_doorPhase != DoorWaitPhase.None)
            return;
        if (string.IsNullOrWhiteSpace(RemainingQtyBox.Text))
            ResetFromStep(3);
        ApplyStepGating();
    }

    private async void ConfirmQuotaBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RemainingQtyBox.Text, out _))
        {
            SetStatus("剩余芯片数须为整数。");
            return;
        }

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法校验用量。");
            return;
        }

        if (!App.Flows.IsFlowActive)
        {
            SetStatus("请先完成机台校验。");
            return;
        }

        var remainingQty = RemainingQtyBox.Text.Trim();
        await RunFlowActionAsync(async () =>
        {
            var (ok, msg, reason) = await Task.Run(() =>
                App.Flows.AdvanceOpQuotaCheck(remainingQty));

            if (!ok)
            {
                ClearQuotaResults();
                return (false, msg);
            }

            if (reason != FlowPauseReason.AwaitingAction)
                return (false, msg);

            BindQuotaResults();
            _steps.CompleteStep(3);
            return (true, "用量校验通过。请点「提交归还焊丝」。");
        });
    }

    private async void SubmitReturnBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_doorPhase == DoorWaitPhase.ReturnSlot)
        {
            await TryCompleteDoorCloseAsync(forceIfNoHardware: true);
            return;
        }

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法提交归还。");
            return;
        }

        if (!App.Flows.IsFlowActive)
        {
            SetStatus("请先完成用量校验。");
            return;
        }

        await RunFlowActionAsync(async () =>
        {
            var (ok, msg, reason) = await Task.Run(() => App.Flows.AdvanceOpSubmitReturnToDoorClose());
            if (!ok)
                return (false, msg);

            if (reason != FlowPauseReason.AwaitingDoorClose)
                return (false, msg);

            _returnSlotId = ParseSlotId(App.Flows.GetField("findReturnSlot.return_slot_id"));
            var returnNo = App.Flows.GetField("findReturnSlot.return_slot_no")?.ToString();
            ReturnSlotText.Text = FormatSlotDisplay(returnNo);
            ReturnSlotHintText.Text = ReturnSlotText.Text;
            _doorPhase = DoorWaitPhase.ReturnSlot;
            _returnDoorWasOpen = false;
            TrackDoorOpenState(_returnSlotId, ref _returnDoorWasOpen);
            await App.SlotHardwarePoll.PollAsync();
            await TryCompleteDoorCloseAsync();
            return (true, msg);
        });
    }

    private async void SubmitIssueBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_doorPhase == DoorWaitPhase.IssueSlot)
        {
            await TryCompleteDoorCloseAsync(forceIfNoHardware: true);
            return;
        }

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法提交领用。");
            return;
        }

        if (!App.Flows.IsFlowActive)
        {
            SetStatus("请先完成归还焊丝存入。");
            return;
        }

        await RunFlowActionAsync(async () =>
        {
            var (ok, msg, reason) = await Task.Run(() => App.Flows.AdvanceOpSubmitIssueToDoorClose());
            if (!ok)
                return (false, msg);

            if (reason != FlowPauseReason.AwaitingDoorClose)
                return (false, msg);

            _issueSlotId = ParseSlotId(App.Flows.GetField("queryMatchedAvailableWire.slot_id"));
            var issueNo = App.Flows.GetField("queryMatchedAvailableWire.slot_no")?.ToString();
            IssueSlotText.Text = FormatSlotDisplay(issueNo);
            IssueSlotHintText.Text = IssueSlotText.Text;
            _doorPhase = DoorWaitPhase.IssueSlot;
            _issueDoorWasOpen = false;
            TrackDoorOpenState(_issueSlotId, ref _issueDoorWasOpen);
            await App.SlotHardwarePoll.PollAsync();
            await TryCompleteDoorCloseAsync();
            return (true, msg);
        });
    }

    private async Task TryCompleteDoorCloseAsync(bool forceIfNoHardware = false)
    {
        if (_doorPhase == DoorWaitPhase.None || _actionInProgress)
            return;

        if (_doorPhase == DoorWaitPhase.ReturnSlot)
        {
            if (!await TryAdvanceReturnDoorClosedAsync(forceIfNoHardware))
                return;

            _doorPhase = DoorWaitPhase.None;
            _returnSlotId = 0;
            App.Bootstrap.FlowSlots.Reload();
            _steps.CompleteStep(4);
            ApplyStepGating();
            SetStatus("归还已提交。请点「提交领用焊丝」。");
            return;
        }

        if (_doorPhase == DoorWaitPhase.IssueSlot)
        {
            if (!await TryAdvanceIssueDoorClosedAsync(forceIfNoHardware))
                return;

            _doorPhase = DoorWaitPhase.None;
            _issueSlotId = 0;
            _flowStarted = false;
            App.Bootstrap.FlowSlots.Reload();
            _steps.CompleteStep(5);
            ApplyStepGating();
            SetStatus("领用已提交，流程完成。可点「重新开始」。");
        }
    }

    private Task<bool> TryAdvanceReturnDoorClosedAsync(bool forceIfNoHardware) =>
        TryAdvanceDoorClosedCoreAsync(
            _returnSlotId, forceIfNoHardware, "closeReturnSlotDoor",
            () => _returnDoorWasOpen, v => _returnDoorWasOpen = v,
            () => App.Flows.AdvanceOpAfterReturnDoorClosed());

    private Task<bool> TryAdvanceIssueDoorClosedAsync(bool forceIfNoHardware) =>
        TryAdvanceDoorClosedCoreAsync(
            _issueSlotId, forceIfNoHardware, "closeIssueSlotDoor",
            () => _issueDoorWasOpen, v => _issueDoorWasOpen = v,
            () => App.Flows.AdvanceOpAfterIssueDoorClosed());

    private async Task<bool> TryAdvanceDoorClosedCoreAsync(
        long slotId,
        bool forceIfNoHardware,
        string expectedNodeId,
        Func<bool> getDoorWasOpen,
        Action<bool> setDoorWasOpen,
        Func<(bool Ok, string Message, FlowPauseReason Reason)> advance)
    {
        if (slotId <= 0 || !App.Flows.IsFlowActive
            || !string.Equals(App.Flows.CurrentNodeId, expectedNodeId, StringComparison.OrdinalIgnoreCase))
            return false;

        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == slotId);
        if (slot is null)
            return false;

        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        App.SlotHardwarePoll.Snapshots.TryGetValue(slot.SlotNo, out var hw);
        if (SlotDoorDisplay.IsDisplayOpen(slot, hw, hwConfigured))
        {
            setDoorWasOpen(true);
            if (!forceIfNoHardware)
                return false;
        }
        else if (forceIfNoHardware)
            setDoorWasOpen(true);

        if (!getDoorWasOpen() && !forceIfNoHardware && hwConfigured)
            return false;

        _actionInProgress = true;
        try
        {
            var (ok, msg, reason) = await Task.Run(advance);
            if (!ok)
            {
                SetStatus(msg);
                App.Flows.EndFlow();
                _flowStarted = false;
                _doorPhase = DoorWaitPhase.None;
                return false;
            }

            return reason == FlowPauseReason.Terminal || ok;
        }
        catch (Exception ex)
        {
            SetStatus($"操作失败：{ex.Message}");
            App.Flows.EndFlow();
            _flowStarted = false;
            _doorPhase = DoorWaitPhase.None;
            return false;
        }
        finally
        {
            _actionInProgress = false;
            ApplyStepGating();
        }
    }

    private void TrackDoorOpenState(long slotId, ref bool doorWasOpen)
    {
        if (slotId <= 0)
            return;
        var slot = App.Bootstrap.FlowSlots.Slots.FirstOrDefault(s => s.SlotId == slotId);
        if (slot is null)
            return;
        var hwConfigured = App.Bootstrap.Hardware.IsConfigured;
        App.SlotHardwarePoll.Snapshots.TryGetValue(slot.SlotNo, out var hw);
        if (SlotDoorDisplay.IsDisplayOpen(slot, hw, hwConfigured))
            doorWasOpen = true;
    }

    private void BindWireQueryResults()
    {
        var spec = App.Flows.GetField("queryWireSpecByLotNo.spec")?.ToString();
        WireSpecText.Text = string.IsNullOrWhiteSpace(spec) ? "—" : spec;

        var matchedLot = App.Flows.GetField("queryMatchedAvailableWire.matched_available_wire_lot_no")?.ToString();
        var matchedSlot = App.Flows.GetField("queryMatchedAvailableWire.slot_no")?.ToString();
        if (string.IsNullOrWhiteSpace(matchedLot))
            MatchedWireText.Text = "—";
        else
            MatchedWireText.Text = string.IsNullOrWhiteSpace(matchedSlot)
                ? matchedLot
                : $"{matchedLot}（{FormatSlotDisplay(matchedSlot)}）";

        var weight = App.Flows.GetField("queryWireReturnedWeightBySpec.return_weight");
        ReturnedWeightText.Text = weight switch
        {
            null => "—",
            double d => $"{d.ToString(CultureInfo.InvariantCulture)}",
            decimal m => $"{m.ToString(CultureInfo.InvariantCulture)}",
            _ => weight.ToString() ?? "—"
        };

        IssueSlotText.Text = FormatSlotDisplay(matchedSlot);
        ReturnSlotText.Text = "待分配";
    }

    private void BindEqpQueryResults()
    {
        var lot = App.Flows.GetField("queryLastProductLotNo.lot")?.ToString();
        ProductLotText.Text = string.IsNullOrWhiteSpace(lot) ? "—" : lot;

        var issueNo = App.Flows.GetField("queryMatchedAvailableWire.slot_no")?.ToString();
        IssueSlotHintText.Text = FormatSlotDisplay(issueNo);
        ReturnSlotHintText.Text = "待分配";
    }

    private void BindQuotaResults()
    {
        var diffObj = App.Flows.GetField("queryWireQuotaCheck.quota_diff")
                      ?? App.Flows.GetField("queryWireQuotaCheck.result");
        var diff = diffObj switch
        {
            double d => d,
            decimal m => (double)m,
            int i => i,
            long l => l,
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
            _ => double.NaN
        };

        if (double.IsNaN(diff))
        {
            QuotaDeltaText.Text = "—";
            QuotaHintText.Text = "";
            QuotaPanel.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF8, 0xF0));
            return;
        }

        QuotaDeltaText.Text = diff.ToString(CultureInfo.InvariantCulture);
        QuotaHintText.Text = "(校验通过)";
        QuotaPanel.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF8, 0xF0));
        QuotaPanel.SetResourceReference(Border.BorderBrushProperty, "SuccessBrush");
    }

    private async Task RunFlowActionAsync(Func<Task<(bool Ok, string Message)>> action)
    {
        if (_actionInProgress)
        {
            SetStatus("请稍候，上一操作仍在处理中…");
            return;
        }

        _actionInProgress = true;
        ApplyStepGating();
        try
        {
            var (ok, msg) = await action();
            SetStatus(msg);
            if (!ok && App.Flows.IsFlowActive && _doorPhase == DoorWaitPhase.None)
            {
                // 失败时由调用方决定是否 EndFlow
            }
        }
        catch (Exception ex)
        {
            SetStatus($"操作失败：{ex.Message}");
        }
        finally
        {
            _actionInProgress = false;
            ApplyStepGating();
        }
    }

    private void ResetFromStep(int stepIndex)
    {
        if (stepIndex <= 0)
            return;

        if (_doorPhase != DoorWaitPhase.None)
            return;

        _steps.ResetFromStep(stepIndex);

        if (stepIndex <= 1)
            ClearWireResults();
        if (stepIndex <= 2)
            ClearEqpResults();
        if (stepIndex <= 3)
            ClearQuotaResults();

        if (stepIndex == 1)
            SetStatus("已清空批号及后续信息，请重新输入归还焊丝批号。");
        else if (stepIndex == 2)
            SetStatus("已清空机台及后续信息，请重新输入机台号。");
        else if (stepIndex == 3)
            SetStatus("已清空剩余芯片及后续信息，请重新输入。");
    }

    private void ClearWireResults()
    {
        WireSpecText.Text = "—";
        MatchedWireText.Text = "—";
        ReturnedWeightText.Text = "—";
        IssueSlotText.Text = "—";
        ReturnSlotText.Text = "—";
    }

    private void ClearEqpResults()
    {
        ProductLotText.Text = "—";
        IssueSlotHintText.Text = "—";
        ReturnSlotHintText.Text = "—";
    }

    private void ClearQuotaResults()
    {
        QuotaDeltaText.Text = "—";
        QuotaHintText.Text = "";
        QuotaPanel.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF8, 0xF0));
        QuotaPanel.SetResourceReference(Border.BorderBrushProperty, "SuccessBrush");
    }

    private static long ParseSlotId(object? value) => value switch
    {
        long l => l,
        int i => i,
        string s when long.TryParse(s, out var x) => x,
        _ => 0
    };

    private static string FormatSlotDisplay(string? slotNo) =>
        string.IsNullOrWhiteSpace(slotNo) ? "—" : SlotWireInfoFormatter.FormatDisplayNo(slotNo);
}
