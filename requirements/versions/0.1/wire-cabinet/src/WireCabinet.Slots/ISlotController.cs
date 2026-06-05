namespace WireCabinet.Slots;

public sealed class SlotDoorState
{
    public long SlotId { get; init; }
    public string SlotNo { get; set; } = "";
    public string UsageType { get; set; } = "";
    public string BizState { get; set; } = "";
    public bool IsOpen { get; set; }
    public bool IsLocked { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public string? WireLotNo { get; set; }
    public string? WireSpec { get; set; }
}

/// <summary>流程引擎使用的格口门控制接口。</summary>
public interface ISlotController
{
    event EventHandler? Changed;
    IReadOnlyList<SlotDoorState> Slots { get; }
    void Reload();
    Task<bool> OpenDoorAsync(long slotId, CancellationToken ct = default);
    Task CloseDoorAsync(long slotId, CancellationToken ct = default);
    void OpenDoor(long slotId);
    void CloseDoor(long slotId);
    long? CloseNextOpenDoor();
    bool AreAllDoorsClosed();
    IReadOnlyList<long> OpenDoorIds();
}
