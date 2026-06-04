using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WireCabinet.Hmi.Views;

public partial class OpView : UserControl
{
    private readonly OpStepFlow _steps = new();
    private bool _flowStarted;

    public OpView()
    {
        InitializeComponent();
        ApplyStepGating();
        SetStatus("请填写操作员 ID、班组、班次，完成后点「校验」。");
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

        OpIdBox.IsEnabled = _steps.IsStepActive(0);
        TeamBox.IsEnabled = _steps.IsStepActive(0);
        ShiftBox.IsEnabled = _steps.IsStepActive(0);
        ValidateOpBtn.IsEnabled = _steps.IsStepActive(0)
            && !string.IsNullOrWhiteSpace(OpIdBox.Text);

        WireLotBox.IsEnabled = _steps.IsStepActive(1);
        QueryWireBtn.IsEnabled = _steps.IsStepActive(1)
            && !string.IsNullOrWhiteSpace(WireLotBox.Text);

        EqpNoBox.IsEnabled = _steps.IsStepActive(2);
        ValidateEqpBtn.IsEnabled = _steps.IsStepActive(2)
            && !string.IsNullOrWhiteSpace(EqpNoBox.Text);

        RemainingQtyBox.IsEnabled = _steps.IsStepActive(3);
        ConfirmQuotaBtn.IsEnabled = _steps.IsStepActive(3)
            && !string.IsNullOrWhiteSpace(RemainingQtyBox.Text);

        SubmitReturnBtn.IsEnabled = _steps.IsStepActive(4);
        SubmitIssueBtn.IsEnabled = _steps.IsStepActive(5);
    }

    private void SetStepOpacity(FrameworkElement panel, int stepIndex) =>
        panel.Opacity = _steps.IsStepFuture(stepIndex) ? 0.45 : 1.0;

    private void RestartBtn_Click(object sender, RoutedEventArgs e)
    {
        _flowStarted = false;
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

    private void ValidateOpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OpIdBox.Text))
            return;

        if (!App.Bootstrap.MesReady)
        {
            SetStatus("MES 未配置，无法校验操作员。");
            return;
        }

        const string flowId = "operator_return_wire_and_issue_available_wire";
        if (!_flowStarted)
        {
            if (!App.Flows.TryBeginFlow(flowId, out var beginMsg))
            {
                SetStatus(beginMsg);
                return;
            }
            _flowStarted = true;
        }

        var team = TeamBox.SelectedItem?.ToString() ?? "";
        var shift = ShiftBox.SelectedItem?.ToString() ?? "";
        App.Flows.FillOpStep0(OpIdBox.Text.Trim(), team, shift);
        var (ok, msg, _) = App.Flows.AdvanceUntilPause();
        if (!ok)
        {
            SetStatus(msg);
            return;
        }

        var name = App.Flows.GetField("queryOPById.operator_name")?.ToString();
        OpNameText.Text = string.IsNullOrWhiteSpace(name) ? "—" : name;
        _steps.CompleteStep(0);
        ApplyStepGating();
        SetStatus("操作员校验通过。请扫描或输入归还焊丝批号，点「查询」。");
    }

    private void WireLotBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WireLotBox.Text))
            ResetFromStep(1);
        ApplyStepGating();
    }

    private void QueryWireBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WireLotBox.Text))
            return;
        WireSpecText.Text = "AlSi-25um (铝硅25微米)";
        MatchedWireText.Text = "同规格 2 个（A03, A07）";
        ReturnedWeightText.Text = "0.85 kg / 卷";
        IssueSlotText.Text = "A03";
        ReturnSlotText.Text = "R05";
        _steps.CompleteStep(1);
        ApplyStepGating();
        SetStatus("焊丝批号查询通过。请输入机台号并点「校验」。");
    }

    private void EqpNoBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EqpNoBox.Text))
            ResetFromStep(2);
        ApplyStepGating();
    }

    private void ValidateEqpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EqpNoBox.Text))
            return;
        ProductLotText.Text = "LOT-2026-0528-0007";
        IssueSlotHintText.Text = IssueSlotText.Text;
        ReturnSlotHintText.Text = ReturnSlotText.Text;
        _steps.CompleteStep(2);
        ApplyStepGating();
        SetStatus("机台校验通过。请输入剩余待焊芯片数并点「用量校验」。");
    }

    private void RemainingQtyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RemainingQtyBox.Text))
            ResetFromStep(3);
        ApplyStepGating();
    }

    private void ConfirmQuotaBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RemainingQtyBox.Text, out var remaining))
        {
            SetStatus("剩余芯片数须为整数。");
            return;
        }

        ExpectedQtyText.Text = (remaining + 40).ToString();
        QuotaDeltaText.Text = "-40";
        QuotaHintText.Text = "(≤500 校验通过)";
        QuotaPanel.Background = (Brush)FindResource("SuccessBrush");
        _steps.CompleteStep(3);
        ApplyStepGating();
        SetStatus("用量校验通过。请点「提交归还焊丝」。");
    }

    private void SubmitReturnBtn_Click(object sender, RoutedEventArgs e)
    {
        _steps.CompleteStep(4);
        ApplyStepGating();
        SetStatus("归还已提交（演示）。请点「提交领用焊丝」完成流程。");
    }

    private void SubmitIssueBtn_Click(object sender, RoutedEventArgs e)
    {
        _steps.CompleteStep(5);
        ApplyStepGating();
        SetStatus("领用已提交（演示）。流程结束，可点「重新开始」。");
    }

    private void ResetFromStep(int stepIndex)
    {
        if (stepIndex <= 0)
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
    }

    private void ClearEqpResults()
    {
        ProductLotText.Text = "—";
        IssueSlotHintText.Text = "—";
        ReturnSlotHintText.Text = "—";
    }

    private void ClearQuotaResults()
    {
        ExpectedQtyText.Text = "—";
        QuotaDeltaText.Text = "—";
        QuotaHintText.Text = "";
        QuotaPanel.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF8, 0xF0));
    }
}
