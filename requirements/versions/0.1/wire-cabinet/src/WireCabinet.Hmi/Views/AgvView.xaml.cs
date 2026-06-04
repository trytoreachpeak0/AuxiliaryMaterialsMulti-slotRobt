using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WireCabinet.Hmi.Services;
using WireCabinet.Rcs;

namespace WireCabinet.Hmi.Views;

public partial class AgvView : UserControl
{
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _polling;

    public AgvView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _pollTimer.Stop();
        _pollTimer.Tick += async (_, _) => await PollTickAsync();
    }

    private AgvServices Agv => App.Agv;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StationCombo.ItemsSource = null;
        MoveBtn.IsEnabled = false;
        ChargeBtn.IsEnabled = false;

        if (!Agv.IsConfigured)
        {
            RcsConfigText.Text = Agv.ConfigHint;
            SetStatus(Agv.ConfigHint);
            return;
        }

        RcsConfigText.Text = Agv.IsConnected ? "RCS 已连接" : "RCS 已配置，请点击刷新登录";
        var stations = Agv.Stations.EnabledWorkStations.ToList();
        StationCombo.ItemsSource = stations;
        if (stations.Count > 0)
            StationCombo.SelectedIndex = 0;

        var chg = Agv.Stations.ChargeStation;
        ChargeTargetText.Text = chg is { RcsDestination: > 0 }
            ? $"充电点：{chg.Name}（站点 {chg.RcsDestination}）"
            : "充电点：未配置（stations.yaml charge_station）";

        MoveBtn.IsEnabled = stations.Count > 0;
        ChargeBtn.IsEnabled = chg is { Enabled: true, RcsDestination: > 0 };

        _pollTimer.Start();
        _ = ConnectAndRefreshAsync();
    }

    private async Task ConnectAndRefreshAsync()
    {
        await Agv.ConnectAsync();
        UpdateStatusPanel();
        SetStatus(Agv.IsConnected ? "RCS 已连接，可下发移动/充电单。" : Agv.LastError ?? "RCS 连接失败");
    }

    private async Task PollTickAsync()
    {
        if (_polling || !Agv.IsConfigured) return;
        _polling = true;
        try
        {
            if (!Agv.IsConnected)
                await Agv.ConnectAsync();
            await Agv.RefreshStatusAsync();
            var policyMsg = await Agv.RunAutoChargePolicyAsync();
            UpdateStatusPanel();
            if (policyMsg is not null)
                SetStatus(policyMsg);
        }
        finally
        {
            _polling = false;
        }
    }

    private void UpdateStatusPanel()
    {
        BatteryText.Text = $"{Agv.LastBatteryPercent:F0}%";
        PositionText.Text = Agv.LastPosition.ToString();
        SysStateText.Text = Agv.LastSysState;
        MoveStateText.Text = Agv.LastMoveState;
        SessionStateText.Text = Agv.Move?.State.ToString() ?? "—";
        RcsConfigText.Text = Agv.IsConnected ? "RCS 已连接" : (Agv.LastError ?? "未连接");
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await ConnectAndRefreshAsync();
    }

    private async void MoveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (StationCombo.SelectedItem is not WorkStationConfig station)
        {
            SetStatus("请选择作业站。");
            return;
        }

        MoveBtn.IsEnabled = false;
        try
        {
            var (ok, msg) = await Agv.MoveToStationAsync(station.RcsDestination);
            SetStatus(msg);
            UpdateStatusPanel();
        }
        finally
        {
            MoveBtn.IsEnabled = true;
        }
    }

    private async void ChargeBtn_Click(object sender, RoutedEventArgs e)
    {
        ChargeBtn.IsEnabled = false;
        try
        {
            var (ok, msg) = await Agv.GoChargeAsync();
            SetStatus(msg);
            UpdateStatusPanel();
        }
        finally
        {
            ChargeBtn.IsEnabled = Agv.Stations.ChargeStation is { RcsDestination: > 0 };
        }
    }

    private void SetStatus(string message)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.SetStatus(message);
    }
}
