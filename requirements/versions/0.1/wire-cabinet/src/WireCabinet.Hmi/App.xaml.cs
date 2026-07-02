using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using WireCabinet.Hmi.Services;
using WireCabinet.Hmi.Views;
using WireCabinet.Slots;

namespace WireCabinet.Hmi;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private bool _servicesInitialized;

    public static AppBootstrap Bootstrap { get; private set; } = null!;
    public static FlowCoordinator Flows { get; private set; } = null!;
    public static WireCabinet.Rcs.StationArrivalGate? StationGate { get; private set; }
    public static AgvServices Agv { get; private set; } = null!;
    public static IoModuleHealthMonitor IoHealth { get; private set; } = null!;
    public static SlotHardwarePollService SlotHardwarePoll { get; private set; } = null!;
    public static MhDoorOnlyService MhDoors { get; private set; } = null!;
    public static DoorOperationGate DoorOps { get; private set; } = null!;
    public static MaintAccessGate MaintAccess { get; private set; } = null!;
    public static HmiUiGateService UiGate { get; private set; } = null!;
    public static AgvDoorInterlockNotifier AgvDoorNotifier { get; private set; } = new();
    public static FlowTraceHub? FlowTrace { get; private set; }

    public static WireMesDiscoRetryTimer? MesDiscoRetry { get; private set; }
    public static KioskOptions Kiosk { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, @"Global\WireCabinet.Hmi", out var createdNew);
        if (!createdNew)
        {
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"启动失败：{args.Exception.Message}", "WireCabinet", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();

        Kiosk = config.GetSection("Ui:Kiosk").Get<KioskOptions>() ?? new KioskOptions();

        Bootstrap = new AppBootstrap(config);
        DoorOps = Bootstrap.DoorOps;
        MaintAccess = new MaintAccessGate(config["MaintAccess:Password"]);
        if (Bootstrap.FlowTraceEnabled)
        {
            FlowTrace = new FlowTraceHub(Bootstrap.FlowTraceOptions, Dispatcher);
            Flows = new FlowCoordinator(Bootstrap, FlowTrace);
        }
        else
            Flows = new FlowCoordinator(Bootstrap);
        var stations = WireCabinet.Rcs.StationConfiguration.Load();
        StationGate = new WireCabinet.Rcs.StationArrivalGate(stations);
        var doors = new WireCabinet.Slots.CabinetDoorStateProvider(Bootstrap.SlotControl);
        Agv = new AgvServices(config, Bootstrap.SlotControl, doors);
        UiGate = new HmiUiGateService();
        IoHealth = new IoModuleHealthMonitor(Bootstrap.Hardware, Bootstrap.SlotIo);
        SlotHardwarePoll = new SlotHardwarePollService(Bootstrap.Hardware, Bootstrap.SlotIo.HealthCheckIntervalMs);
        MhDoors = new MhDoorOnlyService(
            Bootstrap.SlotControl,
            Bootstrap.WireSession,
            Bootstrap.AppDbGateway,
            Bootstrap.Catalog,
            Bootstrap.DiscoSync);

        MesDiscoRetry = new WireMesDiscoRetryTimer(
            Bootstrap.DiscoSync,
            Bootstrap.MesReconciliation,
            TimeSpan.FromMinutes(2));
        MesDiscoRetry.Start();

        var doorReconciler = new DoorStateReconciler(Bootstrap.SlotControl, Bootstrap.Hardware);
        SlotHardwarePoll.SetAfterPoll(async snapshots =>
        {
            var closed = await doorReconciler.ReconcileAsync(snapshots);
            if (closed.Count > 0)
            {
                await MhDoors.NotifySlotsClosedAsync(closed);
                Bootstrap.FlowSlots.Reload();
            }
            else
                MhDoors.TryAutoEndDoorOnlySession();
        });

        base.OnStartup(e);
        _servicesInitialized = true;

        if (FlowTrace is not null)
            new FlowTraceWindow(FlowTrace, Bootstrap.Flows).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        if (_servicesInitialized)
        {
            Agv.Dispose();
            IoHealth.Dispose();
            SlotHardwarePoll.Dispose();
            MesDiscoRetry?.Dispose();
            Bootstrap.Dispose();
        }
        base.OnExit(e);
    }
}
