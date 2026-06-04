using System.Globalization;
using WireCabinet.Slots;

namespace WireCabinet.Hmi.Services;

/// <summary>
/// MH 纯开门：仅更新 door_state/lock_state，不跑 material_handler_open_* 全流程，不修改 biz_state。
/// 与需求范围 §3.3 关门 assume_empty 脱钩，供 HMI 仓门批量控制使用。
/// </summary>
public sealed class MhDoorOnlyService
{
    public const string SessionFlowId = "mh_door_only";

    private readonly ISlotControlService _slots;
    private readonly IWireOperationSession _session;
    private readonly WireCabinet.Data.IAppDb _appDb;
    private readonly WireCabinet.Data.SqlCatalog _catalog;

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

    public bool TryBeginSession(out string message) =>
        _session.TryBegin(SessionFlowId, out message);

    public void EndSession() => _session.End();

    /// <summary>维护视图试开锁：不占焊丝操作会话。</summary>
    public async Task<(bool Ok, string Message)> OpenSingleMaintTrialAsync(string slotNoInput, CancellationToken ct = default)
    {
        var slotNo = await ResolveSlotNoAsync(slotNoInput, ct);
        if (slotNo is null)
            return (false, $"未找到格口「{slotNoInput}」（需已启用）。");

        var result = await _slots.OpenSlotAsync(new OpenSlotRequest
        {
            SlotNo = slotNo,
            Source = "maint_trial",
            OperationLabel = $"试开格口 {slotNo}"
        }, ct);

        return result.Success
            ? (true, $"试开 {result.SlotNo} 成功（仅门/锁）。")
            : (false, result.Message);
    }

    public async Task<(bool Ok, string Message)> OpenSingleAsync(string slotNoInput, CancellationToken ct = default)
    {
        if (!EnsureSession(out var sessionMsg))
            return (false, sessionMsg);

        var slotNo = await ResolveSlotNoAsync(slotNoInput, ct);
        if (slotNo is null)
            return (false, $"未找到格口「{slotNoInput}」（需已启用）。");

        var result = await _slots.OpenSlotAsync(new OpenSlotRequest
        {
            SlotNo = slotNo,
            Source = SessionFlowId,
            OperationLabel = $"打开指定格口 {slotNo}"
        }, ct);

        return result.Success
            ? (true, $"已打开格口 {result.SlotNo}（仅门/锁状态）。")
            : (false, result.Message);
    }

    public Task<(bool Ok, string Message)> OpenAllSlotsAsync(CancellationToken ct = default) =>
        OpenFilteredBatchAsync("all_slots", "打开所有仓门", ct);

    public Task<(bool Ok, string Message)> OpenAvailableWireSlotsAsync(CancellationToken ct = default) =>
        OpenFilteredBatchAsync("available_wire_slots", "打开所有有料仓门", ct);

    public Task<(bool Ok, string Message)> OpenReturnedWireSlotsAsync(CancellationToken ct = default) =>
        OpenFilteredBatchAsync("returned_wire_slots", "打开所有归还焊丝仓门", ct);

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
        if (!EnsureSession(out var sessionMsg))
            return (false, sessionMsg);

        var codes = await ListOpenableSlotCodesAsync(filter, ct);
        if (codes.Count == 0)
            return (true, "没有符合条件的已启用、已接线格口。");

        var batch = await _slots.OpenSlotsBatchAsync(
            codes,
            new BatchOpenOptions { IntervalMs = 400, StopOnFailure = true },
            new OpenSlotRequest { Source = SessionFlowId, OperationLabel = operationLabel },
            ct);

        var opened = batch.Results.Count(r => r.Success);
        var failed = batch.Results.FirstOrDefault(r => !r.Success);
        if (failed is not null)
            return (false, $"批量开门中止：{failed.Message}（已成功 {opened}/{codes.Count}）");

        return (true, $"已打开 {opened} 个格口（仅门/锁状态）。");
    }

    private async Task<(bool Ok, string Message)> OpenFilteredBatchMaintAsync(
        string filter, MaintSideFilter side, string operationLabel, CancellationToken ct)
    {
        var codes = await ListOpenableSlotCodesAsync(filter, ct);
        if (side != MaintSideFilter.All)
        {
            var filtered = new List<string>();
            foreach (var code in codes)
            {
                var dto = await FindBySlotNoAsync(code, ct);
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
                MaintSideFilter.Front => "没有符合条件的前柜已启用、已接线格口。",
                MaintSideFilter.Rear => "没有符合条件的后柜已启用、已接线格口。",
                _ => "没有符合条件的已启用、已接线格口。"
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

    private async Task<List<string>> ListOpenableSlotCodesAsync(string filter, CancellationToken ct)
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
            if (dto is { IsEnabled: true, IoWired: true })
                codes.Add(dto.SlotNo);
        }
        return codes;
    }

    private async Task<string?> ResolveSlotNoAsync(string input, CancellationToken ct)
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
            var dto = await FindBySlotNoAsync(c, ct);
            if (dto is not null) return dto.SlotNo;
        }
        return null;
    }

    private async Task<SlotStatusDto?> FindBySlotNoAsync(string slotNo, CancellationToken ct)
    {
        var all = await _slots.ListSlotsAsync(new SlotListFilter { EnabledOnly = true }, ct);
        return all.FirstOrDefault(s => string.Equals(s.SlotNo, slotNo, StringComparison.OrdinalIgnoreCase));
    }

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
