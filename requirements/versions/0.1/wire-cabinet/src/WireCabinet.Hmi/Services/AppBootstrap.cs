using System.IO;
using Microsoft.Extensions.Configuration;
using WireCabinet.Core;
using WireCabinet.Core.Config;
using WireCabinet.Data;
using WireCabinet.Flows;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

public sealed class AppBootstrap : IDisposable
{
    public SqliteDb AppDb { get; }
    public SqlCatalog Catalog { get; }
    public IMesGateway Mes { get; }
    public IAppDb AppDbGateway { get; }
    public SlotIoConfig SlotIo { get; }
    public ISlotHardwareService Hardware { get; }
    public DoorOperationGate DoorOps { get; }
    public ISlotControlService SlotControl { get; }
    public ISlotController FlowSlots { get; }
    public IWireOperationSession WireSession { get; } = new WireOperationSession();
    public MhInterruptedLoadStore InterruptedLoad { get; }
    public EngineServices EngineServices { get; }
    public IReadOnlyList<FlowDefinition> Flows { get; }
    public MesOptions MesOptions { get; }
    public bool MesReady { get; }

    public AppBootstrap(IConfiguration config)
    {
        var dbPath = config["ApplicationDatabase:Path"];
        if (string.IsNullOrWhiteSpace(dbPath))
            dbPath = CabinetPaths.DefaultAppDbPath;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        var needsInit = !SqliteDb.HasAppSlotTable(dbPath);
        if (needsInit && File.Exists(dbPath) && new FileInfo(dbPath).Length == 0)
            File.Delete(dbPath);
        AppDb = new SqliteDb(dbPath);
        if (needsInit)
            AppDb.Initialize(recreate: false);

        InterruptedLoad = new MhInterruptedLoadStore(AppDb);
        InterruptedLoad.EnsureSchema();

        Catalog = SqlCatalog.Load();
        MesOptions = new MesOptions { ConnectionString = NormalizeMesConnectionString(config["Mes:ConnectionString"]) };
        MesReady = MesOptions.IsConfigured;
        Mes = MesReady
            ? new OracleMesGateway(MesOptions.ConnectionString)
            : new SqliteMockMesGateway(AppDb, new MesFunctionSettings());

        AppDbGateway = new SqliteAppDb(AppDb);
        SlotIo = SlotIoConfig.Load();
        Hardware = new ModbusSlotHardwareService(SlotIo);
        DoorOps = new DoorOperationGate();
        SlotControl = new SlotControlService(AppDb, Hardware, DoorOps);
        FlowSlots = new FlowSlotController(SlotControl, AppDb);

        EngineServices = new EngineServices
        {
            Catalog = Catalog,
            AppDb = AppDbGateway,
            Mes = Mes,
            Slot = FlowSlots,
            SystemAgvNo = config["AgvDispatch:DefaultDeviceKey"] ?? "AGV-01"
        };

        Flows = FlowLoader.LoadAll();
    }

    public void Dispose() => (Hardware as IDisposable)?.Dispose();

    /// <summary>去掉从旧版配置整段粘贴时误带的 OracleConnection= 前缀。</summary>
    private static string NormalizeMesConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var s = raw.Trim();
        const string legacyPrefix = "OracleConnection=";
        if (s.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            s = s[legacyPrefix.Length..].TrimStart();

        return s;
    }
}

public sealed class UnconfiguredMesGateway : IMesGateway
{
    public MesMode Mode => MesMode.Oracle;
    public DbResult Run(SqlCatalogItem item, IDictionary<string, object?> parameters) =>
        DbResult.Failure(item.OracleSql ?? item.Sql, "MES 未配置：请在 appsettings.json 填写 Mes:ConnectionString。");
}
