namespace WireCabinet.Slots.Hardware;

/// <summary>单格口 IO 快照（维护界面按格口展示锁 DI / DO）。</summary>
public sealed class SlotHardwareSnapshot
{
    public string SlotCode { get; init; } = "";
    public string ModuleKey { get; init; } = "";
    public int DoIndex { get; init; }
    public int DiIndex { get; init; }
    public bool Wired { get; init; }
    /// <summary>true=锁闭合，false=锁释放，null=未读或失败。</summary>
    public bool? LockClosed { get; init; }
    public bool? DoActive { get; init; }
    public bool ReadOk { get; init; }
    public DateTime ReadAt { get; init; } = DateTime.UtcNow;
}
