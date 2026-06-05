namespace WireCabinet.Slots;

public sealed class OpenSlotRequest
{
    public long? SlotId { get; init; }
    public string? SlotNo { get; init; }
    public string? OperatorId { get; init; }
    public string Source { get; init; } = "";
    public string? SessionId { get; init; }
    /// <summary>UI 与互斥门禁展示用中文标签。</summary>
    public string? OperationLabel { get; init; }
}

public sealed class OpenSlotResult
{
    public bool Success { get; init; }
    public string SlotNo { get; init; } = "";
    public long SlotId { get; init; }
    public string Message { get; init; } = "";
}

public sealed class BatchOpenOptions
{
    public int IntervalMs { get; init; } = 400;
    public bool StopOnFailure { get; init; } = true;
}

public sealed class BatchOpenResult
{
    public IReadOnlyList<OpenSlotResult> Results { get; init; } = Array.Empty<OpenSlotResult>();
    public bool CompletedAll { get; init; }
}

public sealed class SlotStatusDto
{
    public long SlotId { get; init; }
    public string SlotNo { get; init; } = "";
    public string UsageType { get; init; } = "";
    public string BizState { get; init; } = "";
    public string DoorState { get; init; } = "";
    public string LockState { get; init; } = "";
    public bool IsEnabled { get; init; }
    public bool IoWired { get; init; }
}

public sealed class SlotListFilter
{
    public string? BizState { get; init; }
    public string? UsageType { get; init; }
    public bool? EnabledOnly { get; init; } = true;
}

public interface ISlotControlService
{
    Task<OpenSlotResult> OpenSlotAsync(OpenSlotRequest request, CancellationToken ct = default);
    Task<BatchOpenResult> OpenSlotsBatchAsync(
        IReadOnlyList<string> slotCodes,
        BatchOpenOptions options,
        OpenSlotRequest template,
        CancellationToken ct = default);
    Task<bool> ConfirmDoorClosedAsync(long slotId, CancellationToken ct = default);
    bool AreAllDoorsClosed();
    bool HasUnlockInProgress();
    IReadOnlyList<long> OpenSlotIds();
    Task<SlotStatusDto?> GetSlotStatusAsync(long slotId, CancellationToken ct = default);
    Task<IReadOnlyList<SlotStatusDto>> ListSlotsAsync(SlotListFilter filter, CancellationToken ct = default);
    Task<AgvDispatch.Sdk.Operations.DoorStateInput> GetDoorStateForAgvAsync(CancellationToken ct = default);
    Task<(bool Success, string Message)> SetSlotEnabledAsync(long slotId, bool enabled, CancellationToken ct = default);
}
