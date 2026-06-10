using System.Windows.Threading;
using WireCabinet.Data;

namespace WireCabinet.Hmi.Services;

/// <summary>启动时与定时扫描 wire_mes_disco_sync pending 并重试。</summary>
public sealed class WireMesDiscoRetryTimer : IDisposable
{
    private readonly IWireDiscoEqpSync _sync;
    private readonly MesDiscoReconciliationService _reconcile;
    private readonly DispatcherTimer _timer;

    public WireMesDiscoRetryTimer(IWireDiscoEqpSync sync, MesDiscoReconciliationService reconcile, TimeSpan interval)
    {
        _sync = sync;
        _reconcile = reconcile;
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _ = Task.Run(() =>
        {
            try { _sync.RetryPendingBatch(); } catch { /* 后台重试不阻塞 UI */ }
        });
        _timer.Start();
    }

    private void Tick()
    {
        _ = Task.Run(() =>
        {
            try
            {
                _reconcile.EnqueueAutoRepairForDrift();
                _sync.RetryPendingBatch();
            }
            catch { /* 忽略定时重试异常 */ }
        });
    }

    public void Dispose() => _timer.Stop();
}
