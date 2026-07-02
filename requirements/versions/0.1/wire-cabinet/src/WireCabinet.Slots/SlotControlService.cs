using AgvDispatch.Sdk.Operations;
using Microsoft.Data.Sqlite;
using WireCabinet.Data;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Slots;

public sealed class SlotControlService : ISlotControlService
{
    private readonly SqliteDb _db;
    private readonly ISlotHardwareService _hardware;
    private readonly DoorOperationGate _doorOps;
    private readonly HashSet<long> _unlockInProgress = new();
    private readonly object _gate = new();

    public SlotControlService(SqliteDb db, ISlotHardwareService hardware, DoorOperationGate doorOps)
    {
        _db = db;
        _hardware = hardware;
        _doorOps = doorOps;
    }

    public async Task<OpenSlotResult> OpenSlotAsync(OpenSlotRequest request, CancellationToken ct = default)
    {
        var label = ResolveOperationLabel(request);
        if (!_doorOps.TryEnter(label, out var busyMessage))
            return new OpenSlotResult { Success = false, Message = busyMessage };

        try
        {
            return await OpenSlotCoreAsync(request, ct);
        }
        finally
        {
            _doorOps.Exit();
        }
    }

    public async Task<BatchOpenResult> OpenSlotsBatchAsync(
        IReadOnlyList<string> slotCodes,
        BatchOpenOptions options,
        OpenSlotRequest template,
        CancellationToken ct = default)
    {
        var label = template.OperationLabel ?? "批量开门";
        if (!_doorOps.TryEnter(label, out var busyMessage))
        {
            return new BatchOpenResult
            {
                Results = [new OpenSlotResult { Success = false, Message = busyMessage }],
                CompletedAll = false
            };
        }

        try
        {
            var results = new List<OpenSlotResult>();
            foreach (var code in slotCodes)
            {
                var r = await OpenSlotCoreAsync(new OpenSlotRequest
                {
                    SlotNo = code,
                    OperatorId = template.OperatorId,
                    Source = template.Source,
                    SessionId = template.SessionId,
                    OperationLabel = template.OperationLabel
                }, ct);
                results.Add(r);
                if (!r.Success && options.StopOnFailure)
                    return new BatchOpenResult { Results = results, CompletedAll = false };
                if (options.IntervalMs > 0)
                    await Task.Delay(options.IntervalMs, ct);
            }

            return new BatchOpenResult { Results = results, CompletedAll = true };
        }
        finally
        {
            _doorOps.Exit();
        }
    }

    private async Task<OpenSlotResult> OpenSlotCoreAsync(OpenSlotRequest request, CancellationToken ct)
    {
        var slot = await ResolveSlotAsync(request, ct);
        if (slot is null)
            return new OpenSlotResult { Success = false, Message = "格口不存在。" };
        if (slot.Value.RejectMessage is not null)
            return new OpenSlotResult { Success = false, SlotId = slot.Value.Id, SlotNo = slot.Value.No, Message = slot.Value.RejectMessage };

        lock (_gate) _unlockInProgress.Add(slot.Value.Id);

        try
        {
            if (_hardware.IsConfigured)
            {
                var ok = await _hardware.OpenLockAsync(slot.Value.No, ct);
                if (!ok)
                    return new OpenSlotResult { Success = false, SlotId = slot.Value.Id, SlotNo = slot.Value.No, Message = "开锁失败：硬件未响应。" };
            }

            await UpdateDoorStateAsync(slot.Value.Id, open: true, ct);
            return new OpenSlotResult { Success = true, SlotId = slot.Value.Id, SlotNo = slot.Value.No, Message = "开锁指令已下发。" };
        }
        finally
        {
            lock (_gate) _unlockInProgress.Remove(slot.Value.Id);
        }
    }

    public async Task<(bool Success, string Message)> SetSlotEnabledAsync(long slotId, bool enabled, CancellationToken ct = default)
    {
        var row = await GetSlotRowAsync(slotId, ct);
        if (row is null)
            return (false, "格口不存在。");
        if (!row.Value.Wired)
            return (false, "未接线格口不可切换启用状态。");

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE app_slot SET is_enabled=$e, updated_at=datetime('now')
            WHERE slot_id=$id AND io_wired=1
            """;
        cmd.Parameters.AddWithValue("$e", enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", slotId);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            return (false, "格口状态更新失败。");

        return (true, enabled ? "格口已启用。" : "格口已禁用。");
    }

    private static string ResolveOperationLabel(OpenSlotRequest request) =>
        !string.IsNullOrWhiteSpace(request.OperationLabel)
            ? request.OperationLabel!
            : !string.IsNullOrWhiteSpace(request.Source)
                ? request.Source
                : "开门";

    public async Task<bool> ConfirmDoorClosedAsync(long slotId, CancellationToken ct = default)
    {
        var row = await GetSlotRowAsync(slotId, ct);
        if (row is null) return false;

        if (_hardware.IsConfigured)
        {
            var closed = await _hardware.ReadLockClosedAsync(row.Value.No, ct);
            if (closed == false) return false;
        }

        await UpdateDoorStateAsync(slotId, open: false, ct);
        return true;
    }

    public bool AreAllDoorsClosed()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM app_slot
            WHERE is_enabled=1 AND io_wired=1 AND door_state='open'
            """;
        return Convert.ToInt32(cmd.ExecuteScalar()) == 0;
    }

    public bool HasUnlockInProgress()
    {
        lock (_gate) return _unlockInProgress.Count > 0;
    }

    public IReadOnlyList<long> OpenSlotIds()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT slot_id FROM app_slot WHERE door_state='open'";
        var list = new List<long>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetInt64(0));
        return list;
    }

    public async Task<SlotStatusDto?> GetSlotStatusAsync(long slotId, CancellationToken ct = default)
    {
        var row = await GetSlotRowAsync(slotId, ct);
        return row is null ? null : ToDto(row.Value);
    }

    public async Task<IReadOnlyList<SlotStatusDto>> ListSlotsAsync(SlotListFilter filter, CancellationToken ct = default)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT slot_id, slot_no, usage_type, biz_state, door_state, lock_state, is_enabled, io_wired FROM app_slot WHERE 1=1";
        if (filter.EnabledOnly == true) cmd.CommandText += " AND is_enabled=1";
        if (filter.BizState is not null) { cmd.CommandText += " AND biz_state=$bs"; cmd.Parameters.AddWithValue("$bs", filter.BizState); }
        if (filter.UsageType is not null) { cmd.CommandText += " AND usage_type=$ut"; cmd.Parameters.AddWithValue("$ut", filter.UsageType); }
        cmd.CommandText += " ORDER BY slot_index, slot_no";

        var list = new List<SlotStatusDto>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new SlotStatusDto
            {
                SlotId = r.GetInt64(0),
                SlotNo = r.GetString(1),
                UsageType = r.GetString(2),
                BizState = r.GetString(3),
                DoorState = r.GetString(4),
                LockState = r.GetString(5),
                IsEnabled = r.GetInt64(6) == 1,
                IoWired = r.GetInt64(7) == 1
            });
        }
        return list;
    }

    public async Task<DoorStateInput> GetDoorStateForAgvAsync(CancellationToken ct = default)
    {
        var open = await ListOpenInterlockSlotNosAsync(ct).ConfigureAwait(false);
        var anyOpen = open.Count > 0;
        return new DoorStateInput
        {
            AllDoorsClosed = !anyOpen && !HasUnlockInProgress(),
            AnyDoorOpen = anyOpen
        };
    }

    public async Task<IReadOnlyList<string>> ListOpenInterlockSlotNosAsync(CancellationToken ct = default)
    {
        var allSlots = await ListSlotsAsync(new SlotListFilter { EnabledOnly = false }, ct).ConfigureAwait(false);

        IReadOnlyDictionary<string, SlotHardwareSnapshot> snapshots =
            new Dictionary<string, SlotHardwareSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (_hardware.IsConfigured)
            snapshots = await _hardware.ReadAllWiredSnapshotsAsync(ct).ConfigureAwait(false);

        var open = new List<string>();
        foreach (var slot in allSlots)
        {
            if (SlotInterlockHelper.IsSlotOpenForInterlock(slot, snapshots, _hardware.IsConfigured))
                open.Add(slot.SlotNo);
        }

        return open;
    }

    private async Task UpdateDoorStateAsync(long slotId, bool open, CancellationToken ct)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE app_slot SET door_state=$d, lock_state=$l, updated_at=datetime('now') WHERE slot_id=$id
            """;
        cmd.Parameters.AddWithValue("$d", open ? "open" : "closed");
        cmd.Parameters.AddWithValue("$l", open ? "unlocked" : "locked");
        cmd.Parameters.AddWithValue("$id", slotId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<(long Id, string No, string? RejectMessage)?> ResolveSlotAsync(OpenSlotRequest request, CancellationToken ct)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        if (request.SlotId is > 0)
        {
            cmd.CommandText = "SELECT slot_id, slot_no, biz_state, is_enabled FROM app_slot WHERE slot_id=$id";
            cmd.Parameters.AddWithValue("$id", request.SlotId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.SlotNo))
        {
            cmd.CommandText = "SELECT slot_id, slot_no, biz_state, is_enabled FROM app_slot WHERE slot_no=$no";
            cmd.Parameters.AddWithValue("$no", request.SlotNo);
        }
        else return null;

        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;

        var id = r.GetInt64(0);
        var no = r.GetString(1);
        var bizState = r.GetString(2);
        var isEnabled = r.GetInt64(3) == 1;

        if (!isEnabled && !CanOpenDisabledSlot(request.Source, bizState))
            return (id, no, "格口已禁用，不可用于存料。");

        return (id, no, null);
    }

    private static bool CanOpenDisabledSlot(string source, string bizState) =>
        string.Equals(source, "maint_trial", StringComparison.OrdinalIgnoreCase)
        || bizState is "available_wire" or "returned_wire";

    private async Task<(long Id, string No, string Usage, string Biz, string Door, string Lock, bool En, bool Wired)?> GetSlotRowAsync(long slotId, CancellationToken ct)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT slot_id, slot_no, usage_type, biz_state, door_state, lock_state, is_enabled, io_wired FROM app_slot WHERE slot_id=$id";
        cmd.Parameters.AddWithValue("$id", slotId);
        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return (r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt64(6) == 1, r.GetInt64(7) == 1);
    }

    private static SlotStatusDto ToDto((long Id, string No, string Usage, string Biz, string Door, string Lock, bool En, bool Wired) row) =>
        new()
        {
            SlotId = row.Id,
            SlotNo = row.No,
            UsageType = row.Usage,
            BizState = row.Biz,
            DoorState = row.Door,
            LockState = row.Lock,
            IsEnabled = row.En,
            IoWired = row.Wired
        };
}
