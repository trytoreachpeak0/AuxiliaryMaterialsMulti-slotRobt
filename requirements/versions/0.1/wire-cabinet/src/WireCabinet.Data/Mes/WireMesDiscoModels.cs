namespace WireCabinet.Data;

public enum WireMesDiscoAction { Set, Clear }

public enum WireMesDiscoSyncStatus { Pending, Success, Failed, ManualResolved }

public enum WireMesDiscoFailureKind { Inline, Reconcile }

public sealed record WireMesDiscoPendingRow(
    long Id,
    string WireLotNo,
    WireMesDiscoAction Action,
    WireMesDiscoSyncStatus Status,
    WireMesDiscoFailureKind FailureKind,
    string? LastError,
    int RetryCount,
    DateTime? NextRetryAt,
    DateTime UpdatedAt);

public sealed record WireDiscoSyncResult(bool Success, bool PendingRecorded, string UserMessage);

public sealed record MesDiscoDriftItem(
    string WireLotNo,
    bool OnlyInCabinet,
    bool OnlyInMes,
    bool BlockedByInterrupt,
    string Hint);

public sealed record MesReconAuditEntry(long Id, string Action, string? WireLotNo, string Detail, DateTime CreatedAt);

public static class WireMesDiscoMessages
{
    public const string InlineFailUser = "MES 同步失败，系统将自动重试；若长时间未恢复请到 MES 对账界面。";
    public const string FindingCategory = "MES 同步";
}

/// <summary>中断快照存在时禁止后台自动 SET/CLEAR。</summary>
public interface IWireMesInterruptGuard
{
    bool IsLotBlocked(string wireLotNo);
}
