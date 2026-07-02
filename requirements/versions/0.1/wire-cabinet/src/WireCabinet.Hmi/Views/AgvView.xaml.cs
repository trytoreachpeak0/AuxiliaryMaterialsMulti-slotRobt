using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WireCabinet.Hmi.Services;
using WireCabinet.Rcs;

namespace WireCabinet.Hmi.Views;

public partial class AgvView : UserControl
{
    public AgvView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private AgvServices Agv => App.Agv;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StationCombo.ItemsSource = null;

        if (!Agv.IsConfigured)
        {
            RcsConfigText.Text = Agv.ConfigHint;
            SetStatus(Agv.ConfigHint);
            return;
        }

        RcsConfigText.Text = "RCS 由后台自动连接";
        var stations = Agv.Stations.EnabledWorkStations.ToList();
        StationCombo.ItemsSource = stations;
        if (stations.Count > 0)
            StationCombo.SelectedIndex = 0;

        var chg = Agv.Stations.ChargeStation;
        ChargeTargetText.Text = chg is { RcsDestination: > 0 }
            ? $"充电点：{chg.Name}（站点 {chg.RcsDestination}）"
            : "充电点：未配置（stations.yaml charge_station）";

        var mh = Agv.Stations.ResolveDefaultReturnStation();
        var mhLabel = mh is not null ? mh.Name : "物料间";
        ChargePolicyText.Text =
            $"自动回充：电量 ≤ {Agv.LowBatteryPercent}% 下充电单；≥ {Agv.FullBatteryPercent}% 返航去充电前作业站，否则回 {mhLabel}。";

        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated += OnAgvPollUpdated;

        UpdateStatusPanel();
        if (!Agv.IsConnected)
            _ = ConnectOnceAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.AgvPollUpdated -= OnAgvPollUpdated;
    }

    private void OnAgvPollUpdated() => Dispatcher.Invoke(UpdateStatusPanel);

    private async Task ConnectOnceAsync()
    {
        await Agv.ConnectAsync();
        UpdateStatusPanel();
        SetStatus(Agv.IsConnected ? "RCS 已连接，可下发移动/充电单。" : Agv.LastError ?? "RCS 连接失败");
    }

    private void UpdateStatusPanel()
    {
        BatteryText.Text = $"{Agv.LastBatteryPercent:F0}%";
        PositionText.Text = Agv.LastPositionDisplay;
        LocationText.Text = Agv.LastLocationDisplay;
        SysStateText.Text = Agv.LastSysState;
        MoveStateText.Text = Agv.LastMoveState;
        EmergencyText.Text = Agv.LastEmergencyDisplay;
        SessionStateText.Text = Agv.Move?.State.ToString() ?? "—";
        TaskSummaryText.Text = Agv.LastTaskTargetDisplay;
        OrderStateText.Text = Agv.LastOrderStateDisplay;
        TaskProgressText.Text = Agv.HasBlockingOrder ? $"{Agv.LastProgress}%" : "—";

        RcsConfigText.Text = Agv.IsConnected
            ? "RCS 已连接（后台自动刷新）"
            : (Agv.LastError ?? "RCS 未连接");

        App.UiGate.OnAgvSnapshotRefreshed();
        App.UiGate.TryClearMovementLock();
        UpdateButtonStates();
    }

    private void DisableAllMovementControls()
    {
        MoveBtn.IsEnabled = false;
        ChargeBtn.IsEnabled = false;
        StationCombo.IsEnabled = false;
    }

    private void UpdateButtonStates()
    {
        var stations = Agv.Stations.EnabledWorkStations.Any();
        var chgOk = Agv.Stations.ChargeStation is { Enabled: true, RcsDestination: > 0 };
        var moveLocked = App.UiGate.MovementControlsLocked;
        var canMove = Agv.IsConfigured && Agv.IsConnected && stations && Agv.CanPlaceMoveOrChargeOrder && !moveLocked;
        var canCharge = Agv.IsConfigured && Agv.IsConnected && chgOk && Agv.CanPlaceMoveOrChargeOrder && !moveLocked;

        MoveBtn.IsEnabled = canMove;
        ChargeBtn.IsEnabled = canCharge;
        StationCombo.IsEnabled = canMove || canCharge;
        CancelOrderBtn.IsEnabled = Agv.IsConfigured && Agv.IsConnected && Agv.CanCancelActiveOrder;
        ReleaseEmergencyBtn.IsEnabled = Agv.IsConfigured && Agv.IsConnected && Agv.CanReleaseEmergency;
    }

    private async void MoveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (StationCombo.SelectedItem is not WorkStationConfig station)
        {
            SetStatus("请选择作业站。");
            return;
        }

        if (TryBlockDispatchForOpenDoors())
            return;

        DisableAllMovementControls();
        try
        {
            var (ok, msg) = await Agv.MoveToStationAsync(station.RcsDestination);
            ApplyDispatchResult(ok, msg);
        }
        catch
        {
            UpdateButtonStates();
            throw;
        }
    }

    private async void ChargeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (TryBlockDispatchForOpenDoors())
            return;

        DisableAllMovementControls();
        try
        {
            var (ok, msg) = await Agv.GoChargeAsync();
            ApplyDispatchResult(ok, msg);
        }
        catch
        {
            UpdateButtonStates();
            throw;
        }
    }

    private bool TryBlockDispatchForOpenDoors()
    {
        var door = AgvDoorInterlockAlert.EvaluateBeforeDispatch();
        if (!door.HasOpenDoors)
            return false;

        var message = AgvDoorInterlockAlert.BuildDispatchBlockedMessage(door.OpenSlotLabels);
        ShowFlowError(message);
        return true;
    }

    private void ApplyDispatchResult(bool ok, string msg)
    {
        SetStatus(msg);
        if (!ok && ShouldShowDispatchErrorDialog(msg) && Window.GetWindow(this) is Window owner)
            FlowErrorDialog.Show(owner, msg);

        if (ok)
            App.UiGate.SetMovementLocked(true);
        UpdateStatusPanel();
        if (!ok)
            UpdateButtonStates();
    }

    private static bool ShouldShowDispatchErrorDialog(string msg) =>
        msg == AgvServices.AlreadyAtStationMessage
        || msg == AgvServices.AlreadyAtChargeStationMessage
        || msg.Contains("格口", StringComparison.Ordinal)
        || msg.Contains("开锁", StringComparison.Ordinal);

    private void ShowFlowError(string message)
    {
        SetStatus(message);
        if (Window.GetWindow(this) is Window owner)
            FlowErrorDialog.Show(owner, message);
    }

    private async void CancelOrderBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelOrderBtn.IsEnabled = false;
        try
        {
            var (_, msg) = await Agv.CancelActiveOrderAsync();
            SetStatus(msg);
            UpdateStatusPanel();
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private async void ReleaseEmergencyBtn_Click(object sender, RoutedEventArgs e)
    {
        ReleaseEmergencyBtn.IsEnabled = false;
        try
        {
            var (_, msg) = await Agv.ReleaseEmergencyAsync();
            SetStatus(msg);
            UpdateStatusPanel();
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private void SetStatus(string message)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.SetStatus(message);
    }
}
