using WireCabinet.Hmi.Services;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Views;

/// <summary>维护视图格口卡片：启用/禁用 × 门开/关（底色=启用态，边框=门态）。</summary>
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

        var isOpen = hw.LockClosed == false;
        return BuildEnabledDoorTile(state, middle, state.IsEnabled, isOpen);
    }

    private static MaintSlotTileModel BuildEnabledDoorTile(
        SlotDoorState state,
        string middle,
        bool isEnabled,
        bool isOpen)
    {
        if (isEnabled)
        {
            if (isOpen)
            {
                return new MaintSlotTileModel
                {
                    SlotId = state.SlotId,
                    SlotNo = state.SlotNo,
                    DisplayNo = FormatDisplayNo(state.SlotNo),
                    MiddleText = middle,
                    ShowMiddleRow = true,
                    StatusText = "启用·门开",
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
                DisplayNo = FormatDisplayNo(state.SlotNo),
                MiddleText = middle,
                ShowMiddleRow = true,
                StatusText = "启用·门关",
                BackgroundKey = "SlotLoadedBrush",
                BorderKey = "SlotLoadedBorderBrush",
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotLoadedBorderBrush"
            };
        }

        if (isOpen)
        {
            return new MaintSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = FormatDisplayNo(state.SlotNo),
                MiddleText = middle,
                ShowMiddleRow = true,
                StatusText = "禁用·门开",
                BackgroundKey = "SlotDisabledBrush",
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
            DisplayNo = FormatDisplayNo(state.SlotNo),
            MiddleText = middle,
            ShowMiddleRow = true,
            StatusText = "禁用·门关",
            BackgroundKey = "SlotDisabledBrush",
            BorderKey = "SlotDisabledBorderBrush",
            NoForegroundKey = "TextPrimaryBrush",
            StatusForegroundKey = "SlotDisabledBorderBrush"
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
