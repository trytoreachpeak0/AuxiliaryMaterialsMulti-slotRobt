namespace WireCabinet.Slots.Hardware;

public interface ISlotHardwareService
{
    bool IsConfigured { get; }
    IReadOnlyDictionary<string, bool> ModuleHealth { get; }
    Task<bool> OpenLockAsync(string slotCode, CancellationToken ct = default);
    Task<bool?> ReadLockClosedAsync(string slotCode, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, SlotHardwareSnapshot>> ReadAllWiredSnapshotsAsync(CancellationToken ct = default);
    Task RefreshHealthAsync(CancellationToken ct = default);
}
