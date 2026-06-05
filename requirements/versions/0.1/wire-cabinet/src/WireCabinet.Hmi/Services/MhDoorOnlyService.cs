using System.Globalization;
using WireCabinet.Slots;

namespace WireCabinet.Hmi.Services;

/// <summary>
/// MH 纯开门：不跑 material_handler_open_* 全流程；开门仅更新 door_state/lock_state。
/// 关门后对本会话已打开格口执行 assume_empty（需求范围 §3.3）。
/// </summary>
public sealed class MhDoorOnlyService
{
    public const string SessionFlowId = "mh_door_only";

    private readonly ISlotControlService _slots;
    private readonly IWireOperationSession _session;
    private readonly WireCabinet.Data.IAppDb _appDb;
    private readonly WireCabinet.Data.SqlCatalog _catalog;
    private readonly HashSet<long> _openedInSession = new();
    private readonly object _trackGate = new();

    public event EventHandler<string>? SessionAutoEnded;

    /// <summary>本会话已打开、尚未确认关门的格口。</summary>
    public event EventHandler? OperationLockChanged;

    public MhDoorOnlyService(
        ISlotControlService slots,
        IWireOperationSession session,
        WireCabinet.Data.IAppDb appDb,
        WireCabinet.Data.SqlCatalog catalog)
    {
        _slots = slots;
        _session = session;
        _appDb = appDb;
        _catalog = catalog;
    }

    public bool HasActiveSession => _session.HasActiveSession;

    /// <summary>仅 MH 纯开门（批量/单格）会话；不含存料流程会话。</summary>
    public bool HasActiveDoorOnlySession =>
        _session.HasActiveSession
        && string.Equals(_session.ActiveFlowId, SessionFlowId, StringComparison.OrdinalIgnoreCase);

    public bool HasOpenedSlotsAwaitingClose
    {
        get
        {
            lock (_trackGate)
                return _openedInSession.Count > 0;
        }
    }

    public bool TryBeginSession(out string message) =>
        _session.TryBegin(SessionFlowId, out message);

    public void EndSession()
    {
        lock (_trackGate) _openedInSession.Clear();
        _session.End();
    }

    public void TrackOpenedSlot(long slotId)
    {
        if (slotId <= 0) return;
        lock (_trackGate) _openedInSession.Add(slotId);
        NotifyOperationLockChanged();
    }

    public void UntrackSlot(long slotId)
    {
        lock (_trackGate) _openedInSession.Remove(slotId);
    }

    public async Task NotifySlotsClosedAsync(IReadOnlyList<long> slotIds, CancellationToken ct = default)
    {
        List<long> toAssumeEmpty;
        lock (_trackGate)
        {
            toAssumeEmpty = [];
            foreach (var id in slotIds)
            {
                if (_openedInSession.Remove(id))
                    toAssumeEmpty.Add(id);
            }
        }

        foreach (var id in toAssumeEmpty)
            await AssumeEmptyAsync(id, ct);

        if (toAssumeEmpty.Count > 0)
            NotifyOperationLockChanged();

        TryAutoEndDoorOnlySession();
    }

    private Task AssumeEmptyAsync(long slotId, CancellationToken ct)
    {
        var item = _catalog.Find("app.slot.assume_empty");
        if (item is null)
            return Task.CompletedTask;

        _appDb.Run(item, new Dictionary<string, object?> { ["slot_id"] = slotId });
        return Task.CompletedTask;
    }

    /// <summary>本会话打开格口均已关库后自动结束 mh_door_only 会话。</summary>
    public bool TryAutoEndDoorOnlySession()
    {
        if (!HasActiveDoorOnlySession)
            return false;

        lock (_trackGate)
        {
            if (_openedInSession.Count > 0)
                return false;
        }

        _session.End();
        SessionAutoEnded?.Invoke(this, "格口已关，开门会话已结束。");
        NotifyOperationLockChanged();
        return true;
    }

    /// <summary>根据库/DI 门态清理已过期的会话追踪（门已关但未走 NotifySlotsClosedAsync 时）。</summary>
    public void ReconcileSessionTracking(IReadOnlyList<SlotDoorState> slots,
        IReadOnlyDictionary<string, WireCabinet.Slots.Hardware.SlotHardwareSnapshot> snapshots,
        bool hardwareConfigured)
    {
        if (!HasActiveDoorOnlySession)
            return;

        var changed = false;
        lock (_trackGate)
        {
            if (_openedInSession.Count == 0)
            {
                TryAutoEndDoorOnlySession();
                return;
            }

            var stale = new List<long>();
            foreach (var id in _openedInSession)
            {
                var slot = slots.FirstOrDefault(s => s.SlotId == id);
                if (slot is null)
                {
                    stale.Add(id);
                    continue;
                }

                snapshots.TryGetValue(slot.SlotNo, out var hw);
                if (!MhOperationLock.IsSlotBlockingOpen(slot, hw, hardwareConfigured))
                    stale.Add(id);
            }

            foreach (var id in stale)
            {
                _openedInSession.Remove(id);
                changed = true;
            }
        }

        if (changed)
            NotifyOperationLockChanged();

        TryAutoEndDoorOnlySession();
    }

    /// <summary>维护视图试开锁：不占焊丝操作会话；禁用格亦可试开（需已接线）。</summary>
    public async Task<(bool Ok, string Message)> OpenSingleMaintTrialAsync(string slotNoInput, CancellationToken ct = default)
    {
        var dto = await ResolveSlotDtoAsync(slotNoInput, ct, includeDisabled: true);
        if (dto is null)
            return (false, $"未找到格口「{slotNoInput}」。");
        if (!dto.IoWired)
            return (false, $"格口「{dto.SlotNo}」未接线，不可试开。");

        var result = await _slots.OpenSlotAsync(new OpenSlotRequest
        {
            SlotNo = dto.SlotNo,
            Source = "maint_trial",
            OperationLabel = $"试开格口 {dto.SlotNo}"
        }, ct);

        return result.Success
            ? (true, $"试开 {result.SlotNo} 成功（仅门/锁）。")
            : (false, result.Message);
    }

    public async Task<(bool Ok, string Message)> OpenSingleAsync(string slotNoInput, CancellationToken ct = default)
    {
        if (!EnsureNoOpenDoors(out var doorMsg))
            return (false, doorMsg);

        if (!EnsureSession(out var sessionMsg))
            return (false, sessionMsg);

        var dto = await ResolveSlotDtoAsync(slotNoInput, ct, includeDisabled: true);
        if (dto is null)
            return (false, $"未找到格口「{slotNoInput}」。");
        if (!dto.IoWired)
            return (false, $"格口「{dto.SlotNo}」未接线，不可打开。");
        if (!CanOpenForMhDoor(dto))
            return (false, $"格口「{dto.SlotNo}」已禁用且无料，不可打开。");

        var result = await _slots.OpenSlotAsync(new OpenSlotRequest
        {
            SlotNo = dto.SlotNo,
            Source = SessionFlowId,
            OperationLabel = $"打开指定格口 {dto.SlotNo}"
        }, ct);

        if (result.Success)
        {
            TrackOpenedSlot(result.SlotId);
            NotifySlotStateChanged();
        }

        return result.Success
            ? (true, $"已打开格口 {result.SlotNo}（仅门/锁状态）。")
            : (false, result.Message);
    }

    public Task<(bool Ok, string Message)> OpenAllSlotsAsync(CancellationToken ct = default) =>
        OpenFilteredBatchAsync("all_slots", "打开所有非禁用和有料格口", ct);

    public Task<(bool Ok, string Message)> OpenAvailableWireSlotsAsync(CancellationToken ct = default) =>
        OpenFilteredBatchAsync("available_wire_slots", "打开可用焊丝格口", ct);

    public Task<(bool Ok, string Message)> OpenReturnedWireSlotsAsync(CancellationToken ct = default) =>
        OpenFilteredBatchAsync("returned_wire_slots", "打开所有归还焊丝格口", ct);

    /// <summary>维护视图批量开门：不占焊丝操作会话，不改 biz_state。</summary>
    public Task<(bool Ok, string Message)> OpenAllMaintTrialAsync(CancellationToken ct = default) =>
        OpenFilteredBatchMaintAsync("all_slots", MaintSideFilter.All, "打开所有格口", ct);

    public Task<(bool Ok, string Message)> OpenFrontMaintTrialAsync(CancellationToken ct = default) =>
        OpenFilteredBatchMaintAsync("all_slots", MaintSideFilter.Front, "打开前柜所有格口", ct);

    public Task<(bool Ok, string Message)> OpenRearMaintTrialAsync(CancellationToken ct = default) =>
        OpenFilteredBatchMaintAsync("all_slots", MaintSideFilter.Rear, "打开后柜所有格口", ct);

    private enum MaintSideFilter { All, Front, Rear }

    public async Task<(bool Ok, string Message)> CloseSingleAsync(long slotId, CancellationToken ct = default)
    {
        var ok = await _slots.ConfirmDoorClosedAsync(slotId, ct);
        return ok
            ? (true, $"格口 {slotId} 已确认关门（仅门/锁状态）。")
            : (false, "关门确认失败：锁反馈未闭合或格口不存在。");
    }

    private async Task<(bool Ok, string Message)> OpenFilteredBatchAsync(string filter, string operationLabel, CancellationToken ct)
    {
        if (!EnsureNoOpenDoors(out var doorMsg))
            return (false, doorMsg);

        if (!EnsureSession(out var sessionMsg))
            return (false, sessionMsg);

        var codes = await ListOpenableSlotCodesAsync(filter, ct);
        if (codes.Count == 0)
            return (true, "没有符合条件的已接线格口（禁用空格口不可打开）。");

        var batch = await _slots.OpenSlotsBatchAsync(
            codes,
            new BatchOpenOptions { IntervalMs = 400, StopOnFailure = true },
            new OpenSlotRequest { Source = SessionFlowId, OperationLabel = operationLabel },
            ct);

        var opened = batch.Results.Count(r => r.Success);
        foreach (var r in batch.Results.Where(r => r.Success))
            TrackOpenedSlot(r.SlotId);

        var failed = batch.Results.FirstOrDefault(r => !r.Success);
        if (failed is not null)
            return (false, $"批量开门中止：{failed.Message}（已成功 {opened}/{codes.Count}）");

        if (opened > 0)
            NotifySlotStateChanged();

        return (true, $"已打开 {opened} 个格口（仅门/锁状态）。");
    }

    private async Task<(bool Ok, string Message)> OpenFilteredBatchMaintAsync(
        string filter, MaintSideFilter side, string operationLabel, CancellationToken ct)
    {
        var codes = await ListMaintTrialSlotCodesAsync(filter, ct);
        if (side != MaintSideFilter.All)
        {
            var filtered = new List<string>();
            foreach (var code in codes)
            {
                var dto = await FindBySlotNoAsync(code, ct, includeDisabled: true);
                if (dto is null) continue;
                if (side == MaintSideFilter.Front && CabinetSlotGridController.IsFrontSlot(dto.SlotId, dto.SlotNo))
                    filtered.Add(code);
                else if (side == MaintSideFilter.Rear && CabinetSlotGridController.IsRearSlot(dto.SlotId, dto.SlotNo))
                    filtered.Add(code);
            }
            codes = filtered;
        }

        if (codes.Count == 0)
        {
            var emptyMsg = side switch
            {
                MaintSideFilter.Front => "没有符合条件的前柜已接线格口。",
                MaintSideFilter.Rear => "没有符合条件的后柜已接线格口。",
                _ => "没有符合条件的已接线格口。"
            };
            return (true, emptyMsg);
        }

        var batch = await _slots.OpenSlotsBatchAsync(
            codes,
            new BatchOpenOptions { IntervalMs = 400, StopOnFailure = true },
            new OpenSlotRequest { Source = "maint_trial", OperationLabel = operationLabel },
            ct);

        var opened = batch.Results.Count(r => r.Success);
        var failed = batch.Results.FirstOrDefault(r => !r.Success);
        if (failed is not null)
            return (false, $"批量开门中止：{failed.Message}（已成功 {opened}/{codes.Count}）");

        return (true, $"已打开 {opened} 个格口（仅门/锁，未改库存）。");
    }

    private Task<List<string>> ListOpenableSlotCodesAsync(string filter, CancellationToken ct) =>
        ListSlotCodesAsync(filter, ct, SlotListPolicy.MhDoor);

    /// <summary>维护批量试开：含禁用格，仅要求已接线。</summary>
    private Task<List<string>> ListMaintTrialSlotCodesAsync(string filter, CancellationToken ct) =>
        ListSlotCodesAsync(filter, ct, SlotListPolicy.MaintTrial);

    private enum SlotListPolicy
    {
        /// <summary>MH 开门：已启用，或已禁用但有可取焊丝。</summary>
        MhDoor,
        /// <summary>维护试开：所有已接线格口（含禁用空格）。</summary>
        MaintTrial
    }

    private async Task<List<string>> ListSlotCodesAsync(string filter, CancellationToken ct, SlotListPolicy policy)
    {
        var item = _catalog.Find("app.slot.list_by_filter");
        if (item is null)
            return [];

        var result = _appDb.Run(item, new Dictionary<string, object?> { ["filter"] = filter });
        var codes = new List<string>();
        foreach (var row in result.Rows)
        {
            var id = ToLong(row.GetValueOrDefault("slot_id"));
            if (id <= 0) continue;
            var dto = await _slots.GetSlotStatusAsync(id, ct);
            if (dto is null || !dto.IoWired)
                continue;
            if (policy == SlotListPolicy.MhDoor)
            {
                if (!CanOpenForMhDoor(dto))
                    continue;
            }
            codes.Add(dto.SlotNo);
        }
        return codes;
    }

    private static bool CanOpenForMhDoor(SlotStatusDto dto) =>
        dto.IsEnabled || HasRetrievableWire(dto.BizState);

    private static bool HasRetrievableWire(string bizState) =>
        bizState is "available_wire" or "returned_wire";

    private async Task<SlotStatusDto?> ResolveSlotDtoAsync(string input, CancellationToken ct, bool includeDisabled = false)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        var candidates = new List<string> { trimmed };
        if (!trimmed.Contains('-', StringComparison.Ordinal))
        {
            candidates.Add($"F-{trimmed}");
            candidates.Add($"R-{trimmed}");
        }

        foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dto = await FindBySlotNoAsync(c, ct, includeDisabled);
            if (dto is not null) return dto;
        }
        return null;
    }

    private async Task<SlotStatusDto?> FindBySlotNoAsync(string slotNo, CancellationToken ct, bool includeDisabled = false)
    {
        var all = await _slots.ListSlotsAsync(new SlotListFilter { EnabledOnly = !includeDisabled }, ct);
        return all.FirstOrDefault(s => string.Equals(s.SlotNo, slotNo, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EnsureNoOpenDoors(out string message)
    {
        var block = MhOperationLock.Evaluate(reloadDb: true);
        message = block.Message;
        return !block.IsBlocked;
    }

    private void NotifySlotStateChanged()
    {
        try
        {
            App.Bootstrap.FlowSlots.Reload();
        }
        catch
        {
            // ignore
        }

        NotifyOperationLockChanged();
    }

    private void NotifyOperationLockChanged() =>
        OperationLockChanged?.Invoke(this, EventArgs.Empty);

    private bool EnsureSession(out string message)
    {
        if (_session.HasActiveSession &&
            !string.Equals(_session.ActiveFlowId, SessionFlowId, StringComparison.OrdinalIgnoreCase))
        {
            message = "请先完成或退出当前操作。";
            return false;
        }

        if (_session.HasActiveSession)
        {
            message = "";
            return true;
        }

        return _session.TryBegin(SessionFlowId, out message);
    }

    private static long ToLong(object? value) =>
        value switch
        {
            long l => l,
            int i => i,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) => x,
            _ => 0
        };
}
