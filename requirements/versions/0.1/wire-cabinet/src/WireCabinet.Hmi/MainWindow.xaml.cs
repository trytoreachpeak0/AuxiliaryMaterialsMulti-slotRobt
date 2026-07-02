using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WireCabinet.Hmi.Services;
using WireCabinet.Hmi.Views;
using WireCabinet.Rcs;

namespace WireCabinet.Hmi;

public partial class MainWindow : Window
{
    private readonly OpView _opView = new();
    private readonly MhView _mhView = new();
    private readonly MaintView _maintView = new();
    private readonly MesReconView _mesReconView = new();
    private readonly AgvView _agvView = new();
    private readonly DispatcherTimer _clockTimer;
    private DispatcherTimer? _agvPollTimer;
    private readonly AgvArrivalNavigator _agvArrivalNav = new();
    private bool _agvPolling;

    public event Action? AgvPollUpdated;

    private string _roleLabel = "操作员 OP";
    private string? _flowStatus;
    private RadioButton? _lastRoleRadio;
    private bool _suppressRoleRevert;

    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = _opView;
        _lastRoleRadio = RbOp;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;

        UpdateClock();
        _clockTimer.Start();
        ApplyStatusText();

        if (App.Kiosk.Enabled)
            SourceInitialized += (_, _) => KioskWindowHelper.Apply(this, App.Kiosk.Topmost);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        App.IoHealth.Updated += OnIoHealthUpdated;
        App.IoHealth.Start();
        RenderIoModules(App.IoHealth.Statuses);
        ApplyStatusText();
        StartAgvBackgroundPoll();
        _ = ShowInterruptedLoadAlertIfNeededAsync();
    }

    private async Task ShowInterruptedLoadAlertIfNeededAsync()
    {
        try
        {
            await App.SlotHardwarePoll.PollAsync();
            MhInterruptedLoadStartupAlert.TryShow(this);
            OpInterruptedIssueStartupAlert.TryShow(this);
        }
        catch
        {
            // 弹窗失败不阻塞主界面
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        App.IoHealth.Updated -= OnIoHealthUpdated;
        _clockTimer.Stop();
        _agvPollTimer?.Stop();
    }

    private void StartAgvBackgroundPoll()
    {
        if (!App.Agv.IsConfigured) return;

        var pollMs = Math.Max(1000, App.Agv.RecommendedPollIntervalMs);
        _agvPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(pollMs) };
        _agvPollTimer.Tick += async (_, _) => await AgvPollTickAsync();
        _agvPollTimer.Start();
        _ = AgvPollTickAsync();
    }

    private async Task AgvPollTickAsync()
    {
        if (_agvPolling) return;
        _agvPolling = true;
        try
        {
            var policyMsg = await App.Agv.PollCycleAsync(_agvArrivalNav, station =>
            {
                // PollCycleAsync 在 ConfigureAwait(false) 后于线程池调用回调，必须切回 UI 线程
                Dispatcher.BeginInvoke(() => OnAgvArrivedAtStation(station));
            }).ConfigureAwait(true);

            void ApplyPollUi()
            {
                AgvPollUpdated?.Invoke();
                if (policyMsg is not null)
                    SetStatus(policyMsg);
                if (App.Agv.LastDoorInterlockTick is { } doorTick)
                    App.AgvDoorNotifier.OnTick(this, doorTick);
            }

            if (Dispatcher.CheckAccess())
                ApplyPollUi();
            else
                Dispatcher.Invoke(ApplyPollUi);
        }
        finally
        {
            _agvPolling = false;
        }
    }

    private void OnAgvArrivedAtStation(WorkStationConfig station)
    {
        var dualRole = station.AllowedRoles.Count > 1;
        NavigateToWorkStation(station);
        var preferOp = station.AllowedRoles.Any(r =>
            string.Equals(r, "OP", StringComparison.OrdinalIgnoreCase));
        if (preferOp)
            _opView.FocusStationPrimaryInput();
        else
            _mhView.FocusStationPrimaryInput();

        var hint = dualRole ? "（本站双角色，已切至 OP）" : "";
        if (App.DoorOps.IsBusy)
            SetStatus($"已到站 {station.Name}，已切换工作站界面{hint}；门操作进行中，请注意。");
        else
            SetStatus($"已到站 {station.Name}，已切换工作站界面{hint}。");

        AgvPollUpdated?.Invoke();
    }

    private void OnIoHealthUpdated() =>
        Dispatcher.Invoke(() =>
        {
            RenderIoModules(App.IoHealth.Statuses);
            ApplyStatusText();
        });

    private void RbOp_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressRoleRevert) return;
        TrySwitchRole(() =>
        {
            App.MaintAccess.Lock();
            if (MainContent == null) return;
            MainContent.Content = _opView;
            _roleLabel = "操作员 OP";
            _flowStatus = null;
            _lastRoleRadio = RbOp;
            ApplyStatusText();
            AgvPollUpdated?.Invoke();
        });
    }

    private void RbMh_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressRoleRevert) return;
        TrySwitchRole(() =>
        {
            App.MaintAccess.Lock();
            if (MainContent == null) return;
            MainContent.Content = _mhView;
            _roleLabel = "物料员 MH";
            _flowStatus = null;
            _lastRoleRadio = RbMh;
            ApplyStatusText();
            AgvPollUpdated?.Invoke();
        });
    }

    private void RbMaint_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressRoleRevert) return;
        TrySwitchRole(() =>
        {
            if (MainContent == null) return;

            if (App.MaintAccess.IsGateEnabled && !App.MaintAccess.IsUnlocked)
            {
                var dialog = new MaintPasswordDialog { Owner = this };
                if (dialog.ShowDialog() != true)
                {
                    RevertToLastRole();
                    return;
                }
            }

            MainContent.Content = _maintView;
            _roleLabel = "维护 MAINT";
            _flowStatus = null;
            _lastRoleRadio = RbMaint;
            ApplyStatusText();
        });
    }

    private void RbMesRecon_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressRoleRevert) return;
        TrySwitchRole(() =>
        {
            if (MainContent == null) return;

            if (App.MaintAccess.IsGateEnabled && !App.MaintAccess.IsUnlocked)
            {
                var dialog = new MaintPasswordDialog { Owner = this };
                if (dialog.ShowDialog() != true)
                {
                    RevertToLastRole();
                    return;
                }
            }

            MainContent.Content = _mesReconView;
            _roleLabel = "MES 对账";
            _flowStatus = null;
            _lastRoleRadio = RbMesRecon;
            ApplyStatusText();
        });
    }

    private void RbAgv_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressRoleRevert) return;
        TrySwitchRole(() =>
        {
            App.MaintAccess.Lock();
            if (MainContent == null) return;
            MainContent.Content = _agvView;
            _roleLabel = "控车 AGV";
            _flowStatus = null;
            _lastRoleRadio = RbAgv;
            ApplyStatusText();
        });
    }

    private void TrySwitchRole(Action switchAction)
    {
        if (App.DoorOps.IsBusy)
        {
            MessageBox.Show(
                $"当前正在「{App.DoorOps.ActiveLabel}」，请等待完成后再切换界面。",
                "操作进行中",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RevertToLastRole();
            return;
        }

        switchAction();
    }

    private void RevertToLastRole()
    {
        _suppressRoleRevert = true;
        try
        {
            if (_lastRoleRadio is not null)
                _lastRoleRadio.IsChecked = true;
            else
                RbOp.IsChecked = true;
        }
        finally
        {
            _suppressRoleRevert = false;
        }
    }

    private void ApplyStatusText()
    {
        if (StatusText == null) return;
        StatusText.Text = string.IsNullOrWhiteSpace(_flowStatus)
            ? $"{_roleLabel} | {BuildInfraSummary()}"
            : _flowStatus;
    }

    private string BuildInfraSummary()
    {
        var mes = App.Bootstrap.MesReady ? "MES✓" : "MES×";
        var io = FormatIoSummary(App.IoHealth.Statuses);
        var rcs = App.Agv.IsConnected ? "RCS✓" : (App.Agv.IsConfigured ? "RCS○" : "RCS×");
        return $"{mes} | {io} | {rcs}";
    }

    private static string FormatIoSummary(IReadOnlyList<IoModuleStatus> statuses)
    {
        if (statuses.Count == 0)
            return "IO未配置";

        var checkedCount = statuses.Count(s => s.Online.HasValue);
        var onlineCount = statuses.Count(s => s.Online == true);
        if (checkedCount == 0)
            return $"IO检测中(0/{statuses.Count})";
        if (onlineCount == statuses.Count)
            return $"IO在线({onlineCount}/{statuses.Count})";
        return $"IO {onlineCount}/{statuses.Count}在线";
    }

    private void RenderIoModules(IReadOnlyList<IoModuleStatus> statuses)
    {
        if (IoModulePanel == null) return;
        IoModuleStatusPresenter.RenderCompact(IoModulePanel, statuses);
    }

    private void UpdateClock()
    {
        if (ClockText != null)
            ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    public void SetStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetStatus(message));
            return;
        }

        _flowStatus = message;
        ApplyStatusText();
    }

    /// <summary>车辆到达作业站后切换到 OP/MH 工作站界面（任意当前界面，含 MAINT）。</summary>
    public void NavigateToWorkStation(WorkStationConfig station)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => NavigateToWorkStation(station));
            return;
        }

        App.MaintAccess.Lock();

        _suppressRoleRevert = true;
        try
        {
            var preferOp = station.AllowedRoles.Any(r =>
                string.Equals(r, "OP", StringComparison.OrdinalIgnoreCase));

            if (preferOp)
            {
                MainContent!.Content = _opView;
                _roleLabel = "操作员 OP";
                RbOp.IsChecked = true;
                _lastRoleRadio = RbOp;
            }
            else
            {
                MainContent!.Content = _mhView;
                _roleLabel = "物料员 MH";
                RbMh.IsChecked = true;
                _lastRoleRadio = RbMh;
            }

            _flowStatus = null;
            ApplyStatusText();
        }
        finally
        {
            _suppressRoleRevert = false;
        }
    }
}
