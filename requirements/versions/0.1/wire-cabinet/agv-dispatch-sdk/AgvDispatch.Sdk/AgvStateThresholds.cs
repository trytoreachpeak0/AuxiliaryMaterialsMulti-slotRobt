namespace AgvDispatch.Sdk;

/// <summary>
/// 状态判断阈值（全部可配置，对应原 MainFrm 字符串比较）。
/// </summary>
public sealed class AgvStateThresholds
{
    /// <summary>视为调度在线、可参与判断的 enable / is_online 值（原 "true"）。</summary>
    public IList<string> OnlineTrueValues { get; set; } = new List<string> { "true", "True" };

    /// <summary>系统空闲，可接单（原 sys_state == IDLE）。</summary>
    public IList<string> IdleSysStates { get; set; } = new List<string> { "IDLE" };

    /// <summary>非空闲时需结合门状态暂停（原 toolStripStatusLabel6 != "空闲" 对应 EXECUTING 等）。</summary>
    public IList<string> NonIdleSysStatesForDoorPause { get; set; } = new List<string>
    {
        "EXECUTING", "ERROR", "CHARGING", "PAUSE", "UNAVAILABLE"
    };

    /// <summary>移动已结束或未定义（原 AT_FINISHED / AT_NA）。</summary>
    public IList<string> ReadyMoveStates { get; set; } = new List<string> { "AT_FINISHED", "AT_NA" };

    /// <summary>进程空闲（原 IDLE / INNER_FORCE_IDLE）。</summary>
    public IList<string> ReadyProcStates { get; set; } = new List<string> { "IDLE", "INNER_FORCE_IDLE" };

    /// <summary>订单异常需取消（原 order_state == 9）。</summary>
    public int OrderStateCanceled { get; set; } = 9;

    /// <summary>订单已下待执行（原 order_state == 1）。</summary>
    public int OrderStatePending { get; set; } = 1;

    /// <summary>充电中不可下单（原 CHARGING）。</summary>
    public IList<string> ChargingSysStates { get; set; } = new List<string> { "CHARGING" };

    /// <summary>急停/错误（原 ERROR）。</summary>
    public IList<string> ErrorSysStates { get; set; } = new List<string> { "ERROR" };

    /// <summary>期望仓门位数量（原 closeFlag.Length == 24）。</summary>
    public int ExpectedDoorCount { get; set; } = 24;

    /// <summary>门关闭字符（原 '1'）。</summary>
    public char DoorClosedChar { get; set; } = '1';

    /// <summary>门打开字符（原 '0'）。</summary>
    public char DoorOpenChar { get; set; } = '0';
}
