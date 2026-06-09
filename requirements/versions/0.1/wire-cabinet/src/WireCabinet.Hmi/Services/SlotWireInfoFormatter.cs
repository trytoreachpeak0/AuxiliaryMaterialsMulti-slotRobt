using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>格口焊丝信息栏：批号/规格/状态文案与是否有焊丝判定。</summary>
public static class SlotWireInfoFormatter
{
    public static bool HasWire(SlotDoorState state) =>
        state.BizState is "available_wire" or "returned_wire" or "processing"
        || !string.IsNullOrWhiteSpace(state.WireLotNo)
        || !string.IsNullOrWhiteSpace(state.WireSpec);

    public static string FormatDisplayNo(string slotNo)
    {
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        return slotNo;
    }

    /// <summary>操作员界面格口短标签，与 slot-config display_name_short 一致（前-A01 / 后-A01）。</summary>
    public static string FormatCabinetShortLabel(string slotNo)
    {
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return $"前-{slotNo[2..]}";
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return $"后-{slotNo[2..]}";
        return slotNo;
    }

    public static string FormatWireStatus(
        SlotDoorState state,
        SlotHardwareSnapshot? hw,
        bool hardwareConfigured)
    {
        if (SlotDoorDisplay.IsDisplayOpen(state, hw, hardwareConfigured))
            return "格口已开";

        return state.BizState switch
        {
            "available_wire" => "待发放",
            "returned_wire" => "退回",
            "processing" => "处理中",
            _ => "空闲"
        };
    }

    public static string FormatLotNo(SlotDoorState state) =>
        string.IsNullOrWhiteSpace(state.WireLotNo) ? "—" : state.WireLotNo.Trim();

    public static string FormatSpec(SlotDoorState state) =>
        string.IsNullOrWhiteSpace(state.WireSpec) ? "—" : state.WireSpec.Trim();
}
