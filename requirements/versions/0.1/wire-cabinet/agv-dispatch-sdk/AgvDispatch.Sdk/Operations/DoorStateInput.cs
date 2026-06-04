namespace AgvDispatch.Sdk.Operations;

/// <summary>
/// 外设仓门状态（由业务项目提供，SDK 不读 Modbus）。
/// </summary>
public sealed class DoorStateInput
{
    /// <summary>全部关闭时 true（等价 closeFlag 全为 DoorClosedChar）。</summary>
    public bool AllDoorsClosed { get; init; }

    /// <summary>任一门打开时 true（等价 closeFlag 含 DoorOpenChar）。</summary>
    public bool AnyDoorOpen { get; init; }

    /// <summary>原始门状态串，如 24 位 "111..."。</summary>
    public string? CloseFlag { get; init; }

    public static DoorStateInput FromCloseFlag(string? closeFlag, AgvStateThresholds thresholds)
    {
        if (string.IsNullOrEmpty(closeFlag))
            return new DoorStateInput { CloseFlag = closeFlag, AllDoorsClosed = false, AnyDoorOpen = false };

        var expected = thresholds.ExpectedDoorCount;
        var slice = closeFlag.Length >= expected ? closeFlag[..expected] : closeFlag;
        var allClosed = slice.Length == expected && slice.All(c => c == thresholds.DoorClosedChar);
        var anyOpen = slice.Contains(thresholds.DoorOpenChar);
        return new DoorStateInput { CloseFlag = slice, AllDoorsClosed = allClosed, AnyDoorOpen = anyOpen };
    }
}
