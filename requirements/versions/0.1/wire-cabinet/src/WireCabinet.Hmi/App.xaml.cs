using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using WireCabinet.Hmi.Services;
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

        Bootstrap = new AppBootstrap(config);
        DoorOps = Bootstrap.DoorOps;
        MaintAccess = new MaintAccessGate(config["MaintAccess:Password"]);
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
            Bootstrap.Catalog);

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
            Bootstrap.Dispose();
        }
        base.OnExit(e);
    }
}
