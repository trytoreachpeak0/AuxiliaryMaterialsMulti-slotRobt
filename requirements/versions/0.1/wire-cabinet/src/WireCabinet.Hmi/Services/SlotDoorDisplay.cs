using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>格口门态在 HMI 上的展示规则（与 RCS 联锁一致：有 DI 时以锁反馈为准）。</summary>
public static class SlotDoorDisplay
{
    /// <summary>
    /// 界面是否显示为「已打开」：Modbus 已配置且 DI 读数成功时用锁释放判断，否则回退库表 door_state。
    /// </summary>
    public static bool IsDisplayOpen(SlotDoorState state, SlotHardwareSnapshot? hw, bool hardwareConfigured)
    {
        if (hardwareConfigured && hw is { ReadOk: true, LockClosed: not null })
            return hw.LockClosed == false;
        return state.IsOpen;
    }
}
