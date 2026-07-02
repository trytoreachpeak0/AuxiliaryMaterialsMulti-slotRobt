using WireCabinet.Core;
using WireCabinet.Core.Infrastructure;

namespace WireCabinet.Rcs;

public sealed class WorkStationConfig
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int RcsDestination { get; init; }
    public bool Enabled { get; init; } = true;
    public List<string> AllowedRoles { get; init; } = new();
    public List<string> AllowedWireFlows { get; init; } = new();
}

public sealed class ChargeStationConfig
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int RcsDestination { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed class StationConfiguration
{
    public List<WorkStationConfig> WorkStations { get; init; } = new();
    public ChargeStationConfig? ChargeStation { get; set; }
    public int LowBatteryPercent { get; set; } = 20;
    public int FullBatteryPercent { get; set; } = 95;
    public string DefaultReturnStationCode { get; set; } = "WS_MH_STORE";

    public static StationConfiguration Load()
    {
        var path = CabinetPaths.StationsFile;
        if (!File.Exists(path))
            path = Path.Combine(CabinetPaths.VersionRoot, "station-config", "stations.template.yaml");

        var cfg = new StationConfiguration();
        if (!File.Exists(path)) return cfg;

        var root = Yaml.Parse(File.ReadAllText(path));
        var policy = Yaml.Get(root, "charge_policy");
        if (policy is not null)
        {
            if (int.TryParse(Yaml.Str(policy, "low_battery_percent"), out var low))
                cfg.LowBatteryPercent = low;
            if (int.TryParse(Yaml.Str(policy, "full_battery_percent"), out var full))
                cfg.FullBatteryPercent = full;
            var defaultReturn = Yaml.Str(policy, "default_return_station_code");
            if (!string.IsNullOrWhiteSpace(defaultReturn))
                cfg.DefaultReturnStationCode = defaultReturn;
        }

        foreach (var ws in Yaml.AsList(Yaml.Get(root, "work_stations")) ?? [])
        {
            var enabled = !string.Equals(Yaml.Str(ws, "enabled"), "false", StringComparison.OrdinalIgnoreCase);
            cfg.WorkStations.Add(new WorkStationConfig
            {
                Code = Yaml.Str(ws, "station_code") ?? Yaml.Str(ws, "id") ?? "",
                Name = Yaml.Str(ws, "display_name") ?? Yaml.Str(ws, "name") ?? "",
                RcsDestination = ParseInt(Yaml.Str(ws, "rcs_destination")),
                Enabled = enabled,
                AllowedRoles = Yaml.AsList(Yaml.Get(ws, "allowed_roles"))?.Select(x => x.ToString()!).ToList() ?? new(),
                AllowedWireFlows = Yaml.AsList(Yaml.Get(ws, "allowed_wire_flows"))?.Select(x => x.ToString()!).ToList() ?? new()
            });
        }

        var charge = Yaml.Get(root, "charge_station");
        if (charge is not null)
        {
            cfg.ChargeStation = new ChargeStationConfig
            {
                Code = Yaml.Str(charge, "station_code") ?? "",
                Name = Yaml.Str(charge, "display_name") ?? "充电点",
                RcsDestination = ParseInt(Yaml.Str(charge, "rcs_destination")),
                Enabled = !string.Equals(Yaml.Str(charge, "enabled"), "false", StringComparison.OrdinalIgnoreCase)
            };
        }

        return cfg;
    }

    public IEnumerable<WorkStationConfig> EnabledWorkStations =>
        WorkStations.Where(s => s.Enabled && s.RcsDestination > 0);

    public WorkStationConfig? TryGetWorkStationAt(int rcsDestination) =>
        EnabledWorkStations.FirstOrDefault(s => s.RcsDestination == rcsDestination);

    public WorkStationConfig? ResolveDefaultReturnStation()
    {
        if (!string.IsNullOrWhiteSpace(DefaultReturnStationCode))
        {
            var byCode = EnabledWorkStations.FirstOrDefault(s =>
                string.Equals(s.Code, DefaultReturnStationCode, StringComparison.OrdinalIgnoreCase));
            if (byCode is not null)
                return byCode;
        }

        return EnabledWorkStations.FirstOrDefault(s =>
            s.AllowedRoles.Any(r => string.Equals(r, "MH", StringComparison.OrdinalIgnoreCase)));
    }

    public bool IsChargeRelatedPosition(int position)
    {
        if (position <= 0)
            return false;

        return ChargeStation is { Enabled: true, RcsDestination: > 0 } chg && chg.RcsDestination == position;
    }

    public int? ResolveReturnDestination(int currentPosition)
    {
        if (TryGetWorkStationAt(currentPosition) is { } ws)
            return ws.RcsDestination;

        return ResolveDefaultReturnStation()?.RcsDestination;
    }

    private static int ParseInt(string? s) =>
        int.TryParse(s, out var v) ? v : 0;
}

public sealed class StationArrivalGate
{
    private readonly StationConfiguration _stations;

    public StationArrivalGate(StationConfiguration stations) => _stations = stations;

    public (bool Allowed, string Message) CanRunWireFlow(
        string role,
        string flowId,
        bool isArrived,
        int? currentPosition,
        bool isMoving,
        bool isCharging)
    {
        if (isCharging)
            return (false, "充电中禁止焊丝开锁与写库。");
        if (isMoving)
            return (false, "车辆移动中，请等待到站。");
        if (!isArrived || currentPosition is null)
            return (false, "请先移动至对应作业站。");

        var station = _stations.WorkStations.FirstOrDefault(s => s.RcsDestination == currentPosition);
        if (station is null)
            return (false, $"当前站点 {currentPosition} 不是已配置的作业站。");
        if (!station.AllowedRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
            return (false, $"本站（{station.Name}）仅支持：{string.Join("/", station.AllowedRoles)}。");
        if (!station.AllowedWireFlows.Any(f => string.Equals(f, flowId, StringComparison.OrdinalIgnoreCase)))
            return (false, $"本站不允许流程 {flowId}。");

        return (true, $"已到站：{station.Name}");
    }

    public bool IsMaintenanceFlow(string flowId) =>
        flowId.StartsWith("material_handler_open_", StringComparison.OrdinalIgnoreCase);
}
