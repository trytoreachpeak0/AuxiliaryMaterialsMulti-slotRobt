using WireCabinet.Hmi.Services;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Views;

/// <summary>维护视图格口卡片：IO 点位 + 锁 DI 门态（无门磁、无焊丝规格）。</summary>
public sealed class MaintSlotTileModel
{
    public bool IsPlaceholder { get; init; }
    public long? SlotId { get; init; }
    public string? SlotNo { get; init; }

    public string DisplayNo { get; init; } = "—";
    public string? MiddleText { get; init; }
    public bool ShowMiddleRow { get; init; } = true;
    public string StatusText { get; init; } = "—";

    public string BackgroundKey { get; init; } = "SlotEmptyBrush";
    public string BorderKey { get; init; } = "BorderBrush";
    public double BorderThickness { get; init; } = 1.5;
    public string NoForegroundKey { get; init; } = "TextSecondaryBrush";
    public string StatusForegroundKey { get; init; } = "TextSecondaryBrush";
    public bool StatusBold { get; init; }

    public static MaintSlotTileModel Placeholder() => new() { IsPlaceholder = true, ShowMiddleRow = false };

    public static string FormatIoLabel(SlotIoMappingEntry? mapping)
    {
        if (mapping is null || !mapping.Wired)
            return "—";
        return $"{IoModuleHealthMonitor.ShortLabel(mapping.ModuleKey)}:DO{mapping.DoIndex}/DI{mapping.DiIndex}";
    }

    public static MaintSlotTileModel From(
        SlotDoorState state,
        SlotIoMappingEntry? mapping,
        SlotHardwareSnapshot? hw)
    {
        var displayNo = FormatDisplayNo(state.SlotNo);
        var wired = mapping?.Wired == true;

        if (!wired)
        {
            return new MaintSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                MiddleText = "未接线",
                ShowMiddleRow = true,
                StatusText = "—",
                BackgroundKey = "SlotEmptyBrush",
                BorderKey = "BorderBrush",
                NoForegroundKey = "TextSecondaryBrush",
                StatusForegroundKey = "TextSecondaryBrush"
            };
        }

        var middle = FormatIoLabel(mapping);

        if (hw is null || !hw.ReadOk)
        {
            return new MaintSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                MiddleText = middle,
                ShowMiddleRow = true,
                StatusText = "读数失败",
                BackgroundKey = "SlotReturnedBrush",
                BorderKey = "SlotReturnedBorderBrush",
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotReturnedBorderBrush",
                StatusBold = true
            };
        }

        // 无门磁：门态仅由锁反馈 DI 推断（锁释放 = 门开）
        if (hw.LockClosed == false)
        {
            return new MaintSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                MiddleText = middle,
                ShowMiddleRow = true,
                StatusText = "锁释放（门开）",
                BackgroundKey = "SlotOpenBrush",
                BorderKey = "SlotOpenBorderBrush",
                BorderThickness = 2.5,
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotOpenBorderBrush",
                StatusBold = true
            };
        }

        return new MaintSlotTileModel
        {
            SlotId = state.SlotId,
            SlotNo = state.SlotNo,
            DisplayNo = displayNo,
            MiddleText = middle,
            ShowMiddleRow = true,
            StatusText = hw.LockClosed == true ? "锁闭合（门关）" : "锁 DI 未知",
            BackgroundKey = "SlotLoadedBrush",
            BorderKey = "SlotLoadedBorderBrush",
            NoForegroundKey = "TextPrimaryBrush",
            StatusForegroundKey = "SlotLoadedBorderBrush"
        };
    }

    private static string FormatDisplayNo(string slotNo)
    {
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        return slotNo;
    }
}
