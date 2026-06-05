using System.Collections.Concurrent;
using System.Net.Sockets;
using NModbus;
using NModbus.Device;

namespace WireCabinet.Slots.Hardware;

public sealed class ModbusSlotHardwareService : ISlotHardwareService, IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    private readonly SlotIoConfig _config;
    private readonly ConcurrentDictionary<string, bool> _health = new();
    private readonly ConcurrentDictionary<string, IModbusMaster> _masters = new();

    public ModbusSlotHardwareService(SlotIoConfig config) => _config = config;

    public bool IsConfigured => _config.Modules.Any(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Host));

    public IReadOnlyDictionary<string, bool> ModuleHealth => _health;

    public async Task RefreshHealthAsync(CancellationToken ct = default)
    {
        foreach (var mod in _config.Modules.Where(m => m.Enabled))
        {
            try
            {
                using var tcp = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    await tcp.ConnectAsync(mod.Host, mod.Port, cts.Token);
                    _health[mod.Key] = tcp.Connected;
                }
                catch
                {
                    _health[mod.Key] = false;
                }
            }
            catch
            {
                _health[mod.Key] = false;
            }
        }
    }

    public async Task<bool> OpenLockAsync(string slotCode, CancellationToken ct = default)
    {
        var map = _config.FindMapping(slotCode);
        if (map is null || !map.Wired) return false;
        var mod = _config.FindModule(map.ModuleKey);
        if (mod is null || !mod.Enabled) return false;

        var master = await GetMasterAsync(mod, ct).ConfigureAwait(false);
        if (master is null) return false;

        try
        {
            var coil = (ushort)(_config.DoStart + map.DoIndex - 1);
            await master.WriteSingleCoilAsync(mod.UnitId, coil, true).ConfigureAwait(false);
            return true;
        }
        catch
        {
            DropMaster(mod.Key);
            return false;
        }
    }

    public async Task<bool?> ReadLockClosedAsync(string slotCode, CancellationToken ct = default)
    {
        var map = _config.FindMapping(slotCode);
        if (map is null || !map.Wired) return null;
        var mod = _config.FindModule(map.ModuleKey);
        if (mod is null || !mod.Enabled) return null;

        var master = await GetMasterAsync(mod, ct).ConfigureAwait(false);
        if (master is null) return null;

        try
        {
            var input = (ushort)(_config.DiStart + map.DiIndex - 1);
            var inputs = await master.ReadInputsAsync(mod.UnitId, input, 1).ConfigureAwait(false);
            if (inputs.Length == 0) return null;
            var diOn = inputs[0];
            return _config.DiActiveMeansLocked ? diOn : !diOn;
        }
        catch
        {
            DropMaster(mod.Key);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, SlotHardwareSnapshot>> ReadAllWiredSnapshotsAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, SlotHardwareSnapshot>(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        if (!IsConfigured)
        {
            foreach (var map in _config.Mappings.Where(m => m.Wired))
            {
                result[map.SlotCode] = new SlotHardwareSnapshot
                {
                    SlotCode = map.SlotCode,
                    ModuleKey = map.ModuleKey,
                    DoIndex = map.DoIndex,
                    DiIndex = map.DiIndex,
                    Wired = true,
                    ReadOk = false,
                    ReadAt = now
                };
            }
            return result;
        }

        var diByModule = new Dictionary<string, bool[]?>(StringComparer.OrdinalIgnoreCase);
        var doByModule = new Dictionary<string, bool[]?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in _config.Modules.Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Host)))
        {
            try
            {
                var master = await GetMasterAsync(mod, ct).ConfigureAwait(false);
                if (master is null)
                {
                    diByModule[mod.Key] = null;
                    doByModule[mod.Key] = null;
                    continue;
                }

                var diStart = (ushort)_config.DiStart;
                var doStart = (ushort)_config.DoStart;
                var di = await master.ReadInputsAsync(mod.UnitId, diStart, 16).ConfigureAwait(false);
                var coils = await master.ReadCoilsAsync(mod.UnitId, doStart, 16).ConfigureAwait(false);
                diByModule[mod.Key] = di;
                doByModule[mod.Key] = coils;
            }
            catch
            {
                DropMaster(mod.Key);
                diByModule[mod.Key] = null;
                doByModule[mod.Key] = null;
            }
        }

        foreach (var map in _config.Mappings.Where(m => m.Wired))
        {
            bool? lockClosed = null;
            bool? doActive = null;
            var readOk = false;

            if (diByModule.TryGetValue(map.ModuleKey, out var diArr) && diArr is not null
                && map.DiIndex >= 1 && map.DiIndex <= diArr.Length)
            {
                var diOn = diArr[map.DiIndex - 1];
                lockClosed = _config.DiActiveMeansLocked ? diOn : !diOn;
                readOk = true;
            }

            if (doByModule.TryGetValue(map.ModuleKey, out var doArr) && doArr is not null
                && map.DoIndex >= 1 && map.DoIndex <= doArr.Length)
            {
                doActive = doArr[map.DoIndex - 1];
                readOk = true;
            }

            result[map.SlotCode] = new SlotHardwareSnapshot
            {
                SlotCode = map.SlotCode,
                ModuleKey = map.ModuleKey,
                DoIndex = map.DoIndex,
                DiIndex = map.DiIndex,
                Wired = true,
                LockClosed = lockClosed,
                DoActive = doActive,
                ReadOk = readOk,
                ReadAt = now
            };
        }

        return result;
    }

    private void DropMaster(string moduleKey)
    {
        if (_masters.TryRemove(moduleKey, out var master) && master is IDisposable disposable)
            disposable.Dispose();
    }

    private async Task<IModbusMaster?> GetMasterAsync(IoModuleConfig mod, CancellationToken ct = default)
    {
        if (_masters.TryGetValue(mod.Key, out var cached))
            return cached;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ConnectTimeout);
            var client = new TcpClient();
            await client.ConnectAsync(mod.Host, mod.Port, cts.Token).ConfigureAwait(false);
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);

            var added = _masters.GetOrAdd(mod.Key, master);
            if (!ReferenceEquals(added, master) && master is IDisposable extra)
                extra.Dispose();
            return added;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var kv in _masters)
        {
            if (kv.Value is IDisposable d) d.Dispose();
        }
        _masters.Clear();
    }
}
