using System.Windows.Threading;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

public sealed record IoModuleStatus(string Key, string ShortLabel, string Host, int Port, bool? Online);

/// <summary>定时探测 IO 模块 Modbus TCP 端口，供状态栏展示。</summary>
public sealed class IoModuleHealthMonitor : IDisposable
{
    private readonly ISlotHardwareService _hardware;
    private readonly IReadOnlyList<IoModuleConfig> _modules;
    private readonly DispatcherTimer _timer;
    private bool _refreshing;

    public IoModuleHealthMonitor(ISlotHardwareService hardware, SlotIoConfig config)
    {
        _hardware = hardware;
        _modules = config.Modules
            .Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Host))
            .ToList();
        IntervalMs = config.HealthCheckIntervalMs > 0 ? config.HealthCheckIntervalMs : 2000;
        Statuses = BuildStatuses();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IntervalMs) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public int IntervalMs { get; }
    public IReadOnlyList<IoModuleStatus> Statuses { get; private set; }
    public event Action? Updated;

    public void Start()
    {
        _ = RefreshAsync();
        _timer.Start();
    }

    public async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            if (_hardware.IsConfigured)
                await _hardware.RefreshHealthAsync();
            Statuses = BuildStatuses();
            Updated?.Invoke();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private IReadOnlyList<IoModuleStatus> BuildStatuses()
    {
        if (!_hardware.IsConfigured)
            return Array.Empty<IoModuleStatus>();

        var health = _hardware.ModuleHealth;
        return _modules.Select(m =>
        {
            bool? online = health.TryGetValue(m.Key, out var ok) ? ok : null;
            return new IoModuleStatus(m.Key, ShortLabel(m.Key), m.Host, m.Port, online);
        }).ToList();
    }

    public static string ShortLabel(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey)) return "?";
        var i = moduleKey.LastIndexOf(':');
        return i >= 0 && i < moduleKey.Length - 1
            ? moduleKey[(i + 1)..]
            : moduleKey;
    }

    public void Dispose() => _timer.Stop();
}
