using WireCabinet.Core;
using WireCabinet.Core.Infrastructure;

namespace WireCabinet.Slots.Hardware;

public sealed class SlotIoMappingEntry
{
    public string SlotCode { get; init; } = "";
    public string DoorPosition { get; init; } = "";
    public string ModuleKey { get; init; } = "";
    public int DoIndex { get; init; }
    public int DiIndex { get; init; }
    public bool Wired { get; init; }
    public bool EnabledDefault { get; init; } = true;
}

public sealed class IoModuleConfig
{
    public string Key { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; } = 502;
    public byte UnitId { get; init; } = 1;
    public bool Enabled { get; init; } = true;
}

public sealed class SlotIoConfig
{
    public int DoStart { get; set; } = 100;
    public int DiStart { get; set; } = 10200;
    public int HealthCheckIntervalMs { get; set; } = 2000;
    public bool DiActiveMeansLocked { get; set; } = true;
    public List<IoModuleConfig> Modules { get; init; } = new();
    public List<SlotIoMappingEntry> Mappings { get; init; } = new();

    public static SlotIoConfig Load()
    {
        var cfg = new SlotIoConfig();
        var mapPath = CabinetPaths.SlotIoMappingFile;
        if (File.Exists(mapPath))
        {
            var mapRoot = Yaml.Parse(File.ReadAllText(mapPath));
            foreach (var item in Yaml.AsList(Yaml.Get(mapRoot, "mappings")) ?? [])
            {
                cfg.Mappings.Add(new SlotIoMappingEntry
                {
                    SlotCode = Yaml.Str(item, "slot_code") ?? "",
                    DoorPosition = Yaml.Str(item, "door_position") ?? "",
                    ModuleKey = Yaml.Str(item, "module_key") ?? "",
                    DoIndex = int.TryParse(Yaml.Str(item, "do_index"), out var d) ? d : 0,
                    DiIndex = int.TryParse(Yaml.Str(item, "di_index"), out var di) ? di : 0,
                    Wired = string.Equals(Yaml.Str(item, "wired"), "true", StringComparison.OrdinalIgnoreCase),
                    EnabledDefault = !string.Equals(Yaml.Str(item, "enabled_default"), "false", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        var ioPath = File.Exists(CabinetPaths.IoModulesFile)
            ? CabinetPaths.IoModulesFile
            : Path.Combine(CabinetPaths.SlotConfigDir, "io-modules.template.yaml");
        if (File.Exists(ioPath))
        {
            var ioRoot = Yaml.Parse(File.ReadAllText(ioPath));
            var modbus = Yaml.Get(ioRoot, "modbus");
            if (int.TryParse(Yaml.Str(modbus, "health_check_interval_ms"), out var hci) && hci > 0)
                cfg.HealthCheckIntervalMs = hci;
            var addr = Yaml.Get(modbus, "addressing");
            cfg.DoStart = int.TryParse(Yaml.Str(addr, "do_start"), out var ds) ? ds : 100;
            cfg.DiStart = int.TryParse(Yaml.Str(addr, "di_start"), out var dis) ? dis : 10200;
            var diSem = Yaml.Get(ioRoot, "di_semantics");
            cfg.DiActiveMeansLocked = !string.Equals(Yaml.Str(diSem, "di_active_means_locked"), "false", StringComparison.OrdinalIgnoreCase);

            foreach (var m in Yaml.AsList(Yaml.Get(ioRoot, "modules")) ?? [])
            {
                cfg.Modules.Add(new IoModuleConfig
                {
                    Key = Yaml.Str(m, "key") ?? "",
                    Host = Yaml.Str(m, "host") ?? "",
                    Port = int.TryParse(Yaml.Str(m, "port"), out var p) ? p : 502,
                    UnitId = byte.TryParse(Yaml.Str(m, "unit_id"), out var u) ? u : (byte)1,
                    Enabled = !string.Equals(Yaml.Str(m, "enabled"), "false", StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        return cfg;
    }

    public SlotIoMappingEntry? FindMapping(string slotCode) =>
        Mappings.FirstOrDefault(m => string.Equals(m.SlotCode, slotCode, StringComparison.OrdinalIgnoreCase));

    public IoModuleConfig? FindModule(string key) =>
        Modules.FirstOrDefault(m =>
            string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
}
