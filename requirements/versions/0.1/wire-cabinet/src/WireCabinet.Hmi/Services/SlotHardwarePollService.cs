using System.Windows.Threading;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>轮询 Modbus DI/DO 快照，供维护视图格口网格着色。</summary>
public sealed class SlotHardwarePollService : IDisposable
{
    private readonly ISlotHardwareService _hardware;
    private readonly DispatcherTimer _timer;
    private IReadOnlyDictionary<string, SlotHardwareSnapshot> _snapshots =
        new Dictionary<string, SlotHardwareSnapshot>(StringComparer.OrdinalIgnoreCase);

    public SlotHardwarePollService(ISlotHardwareService hardware, int intervalMs = 2000)
    {
        _hardware = hardware;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public event EventHandler? Updated;

    public IReadOnlyDictionary<string, SlotHardwareSnapshot> Snapshots => _snapshots;

    public void Start()
    {
        _ = PollAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public async Task PollAsync()
    {
        try
        {
            _snapshots = await _hardware.ReadAllWiredSnapshotsAsync();
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // 保持上次快照
        }
    }

    public void Dispose() => _timer.Stop();
}
