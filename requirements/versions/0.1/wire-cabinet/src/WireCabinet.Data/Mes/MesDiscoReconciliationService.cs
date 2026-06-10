namespace WireCabinet.Data;

public sealed class MesDiscoReconciliationService
{
    private readonly IAppDb _appDb;
    private readonly IMesGateway _mes;
    private readonly SqlCatalog _catalog;
    private readonly WireDiscoEqpSyncService _sync;
    private readonly IWireMesInterruptGuard? _interruptGuard;

    public MesDiscoReconciliationService(
        IAppDb appDb,
        IMesGateway mes,
        SqlCatalog catalog,
        WireDiscoEqpSyncService sync,
        IWireMesInterruptGuard? interruptGuard = null)
    {
        _appDb = appDb;
        _mes = mes;
        _catalog = catalog;
        _sync = sync;
        _interruptGuard = interruptGuard;
    }

    public IReadOnlyList<MesDiscoDriftItem> ComputeDrift()
    {
        var cabinetLots = LoadCabinetLots();
        var mesLots = LoadMesTaggedLots(_sync.DiscoEqpNo);

        var all = cabinetLots.Union(mesLots, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var drift = new List<MesDiscoDriftItem>();

        foreach (var lot in all)
        {
            var inCab = cabinetLots.Contains(lot, StringComparer.OrdinalIgnoreCase);
            var inMes = mesLots.Contains(lot, StringComparer.OrdinalIgnoreCase);
            if (inCab == inMes)
                continue;

            var blocked = _interruptGuard?.IsLotBlocked(lot) == true;
            var hint = blocked
                ? "存在 MH/OP 中断快照 → 仅人工处理"
                : inCab ? "应对 MES SET" : "应对 MES CLEAR";

            drift.Add(new MesDiscoDriftItem(lot, OnlyInCabinet: inCab && !inMes, OnlyInMes: inMes && !inCab, blocked, hint));
        }

        return drift;
    }

    public int EnqueueAutoRepairForDrift()
    {
        var drift = ComputeDrift();
        var count = 0;
        foreach (var item in drift.Where(d => !d.BlockedByInterrupt))
        {
            if (item.OnlyInCabinet)
            {
                _sync.TryMarkInCabinet(item.WireLotNo, WireMesDiscoFailureKind.Reconcile);
                count++;
            }
            else if (item.OnlyInMes)
            {
                _sync.TryClearFromCabinet(item.WireLotNo, WireMesDiscoFailureKind.Reconcile);
                count++;
            }
        }
        return count;
    }

    private HashSet<string> LoadCabinetLots()
    {
        var result = _appDb.RunRaw(
            """
            SELECT wire_lot_no FROM app_slot
            WHERE biz_state = 'available_wire'
              AND wire_lot_no IS NOT NULL AND TRIM(wire_lot_no) != '';
            """,
            new Dictionary<string, object?>(),
            isWrite: false);

        return result.Rows
            .Select(r => r.GetValueOrDefault("wire_lot_no")?.ToString()?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private HashSet<string> LoadMesTaggedLots(string discoEqpNo)
    {
        var item = ResolveMesListItem();
        if (item is null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = _mes.Run(item, new Dictionary<string, object?> { ["disco_eqp_no"] = discoEqpNo });
        return result.Rows
            .Select(r => r.GetValueOrDefault("sublot")?.ToString()?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private SqlCatalogItem? ResolveMesListItem()
    {
        if (_mes.Mode == MesMode.Oracle)
            return _catalog.Find("mes.wire.list_by_disco_eqp_no");

        return _catalog.Find("mes_mock.wire.list_by_disco_eqp_no")
               ?? _catalog.Find("mes.wire.list_by_disco_eqp_no");
    }
}
