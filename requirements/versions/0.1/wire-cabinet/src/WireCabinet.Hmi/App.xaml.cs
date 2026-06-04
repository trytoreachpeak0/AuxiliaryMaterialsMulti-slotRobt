using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using WireCabinet.Hmi.Services;
using WireCabinet.Slots;

namespace WireCabinet.Hmi;

public partial class App : Application
{
    public static AppBootstrap Bootstrap { get; private set; } = null!;
    public static FlowCoordinator Flows { get; private set; } = null!;
    public static WireCabinet.Rcs.StationArrivalGate? StationGate { get; private set; }
    public static AgvServices Agv { get; private set; } = null!;
    public static IoModuleHealthMonitor IoHealth { get; private set; } = null!;
    public static SlotHardwarePollService SlotHardwarePoll { get; private set; } = null!;
    public static MhDoorOnlyService MhDoors { get; private set; } = null!;
    public static DoorOperationGate DoorOps { get; private set; } = null!;
    public static MaintAccessGate MaintAccess { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // #region agent log
        DispatcherUnhandledException += (_, args) =>
        {
            WireCabinet.Core.DebugLog.Write("H5", "App.DispatcherUnhandledException", args.Exception.Message,
                new { type = args.Exception.GetType().Name, stack = args.Exception.StackTrace });
            MessageBox.Show($"启动失败：{args.Exception.Message}", "WireCabinet", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };
        // #endregion

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
        IoHealth = new IoModuleHealthMonitor(Bootstrap.Hardware, Bootstrap.SlotIo);
        SlotHardwarePoll = new SlotHardwarePollService(Bootstrap.Hardware, Bootstrap.SlotIo.HealthCheckIntervalMs);
        MhDoors = new MhDoorOnlyService(
            Bootstrap.SlotControl,
            Bootstrap.WireSession,
            Bootstrap.AppDbGateway,
            Bootstrap.Catalog);

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Agv.Dispose();
        IoHealth.Dispose();
        SlotHardwarePoll.Dispose();
        Bootstrap.Dispose();
        base.OnExit(e);
    }
}
