using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WireCabinet.Hmi.Services;
using WireCabinet.Hmi.Views;

namespace WireCabinet.Hmi;

public partial class MainWindow : Window
{
    private readonly OpView _opView = new();
    private readonly MhView _mhView = new();
    private readonly MaintView _maintView = new();
    private readonly AgvView _agvView = new();
    private readonly DispatcherTimer _clockTimer;

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
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        App.IoHealth.Updated += OnIoHealthUpdated;
        App.IoHealth.Start();
        RenderIoModules(App.IoHealth.Statuses);
        ApplyStatusText();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        App.IoHealth.Updated -= OnIoHealthUpdated;
        _clockTimer.Stop();
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
        _flowStatus = message;
        ApplyStatusText();
    }
}
