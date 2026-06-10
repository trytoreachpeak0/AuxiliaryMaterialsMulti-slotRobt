using WireCabinet.Data;

namespace WireCabinet.Hmi.Services;

/// <summary>MH/OP 中断快照守卫：存在未清除快照时禁止后台自动 MES 修复。</summary>
public sealed class WireMesInterruptGuard : IWireMesInterruptGuard
{
    private readonly MhInterruptedLoadStore _mh;
    private readonly OpInterruptedIssueStore _op;

    public WireMesInterruptGuard(MhInterruptedLoadStore mh, OpInterruptedIssueStore op)
    {
        _mh = mh;
        _op = op;
    }

    public bool IsLotBlocked(string wireLotNo)
    {
        var lot = wireLotNo.Trim();
        if (string.IsNullOrEmpty(lot))
            return false;

        var mh = _mh.TryGet();
        if (mh is not null && string.Equals(mh.WireLotNo, lot, StringComparison.OrdinalIgnoreCase))
            return true;

        return _op.HasLot(lot);
    }
}
