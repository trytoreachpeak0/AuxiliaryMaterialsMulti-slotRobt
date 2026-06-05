using WireCabinet.Hmi.Services;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Views;

/// <summary>MH 格口卡片展示模型（由 app_slot 状态映射到主题资源键与文案）。</summary>
public sealed class MhSlotTileModel
{
    public bool IsPlaceholder { get; init; }
    public long? SlotId { get; init; }
    public string? SlotNo { get; init; }

    public string DisplayNo { get; init; } = "—";
    public string? TypeText { get; init; }
    /// <summary>是否在格口卡片中显示批号行（有料/退回/开门等）。</summary>
    public bool ShowSpecRow { get; init; }
    public string StatusText { get; init; } = "—";

    public string BackgroundKey { get; init; } = "SlotEmptyBrush";
    public string BorderKey { get; init; } = "BorderBrush";
    public double BorderThickness { get; init; } = 1.5;
    public string NoForegroundKey { get; init; } = "TextSecondaryBrush";
    public string StatusForegroundKey { get; init; } = "TextSecondaryBrush";
    public bool StatusBold { get; init; }

    public static MhSlotTileModel Placeholder() => new() { IsPlaceholder = true };

    public static MhSlotTileModel From(
        SlotDoorState state,
        SlotHardwareSnapshot? hw = null,
        bool hardwareConfigured = false)
    {
        var displayNo = FormatDisplayNo(state.SlotNo);
        var lotText = WireLotDisplay(state);
        var isOpen = SlotDoorDisplay.IsDisplayOpen(state, hw, hardwareConfigured);
        var showSpec = ShouldShowLotRow(state, isOpen);

        if (!state.IsEnabled)
            return BuildDisabledTile(state, displayNo, lotText, showSpec, isOpen);

        if (isOpen)
        {
            return new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = lotText,
                ShowSpecRow = showSpec,
                StatusText = "● 格口已开",
                BackgroundKey = "SlotOpenBrush",
                BorderKey = "SlotOpenBorderBrush",
                BorderThickness = 2.5,
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotOpenBorderBrush",
                StatusBold = true
            };
        }

        return state.BizState switch
        {
            "available_wire" => new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = lotText,
                ShowSpecRow = true,
                StatusText = WireSpecDisplay(state),
                BackgroundKey = "SlotLoadedBrush",
                BorderKey = "SlotLoadedBorderBrush",
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotLoadedBorderBrush"
            },
            "returned_wire" => new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = lotText,
                ShowSpecRow = true,
                StatusText = WireSpecDisplay(state),
                BackgroundKey = "SlotReturnedBrush",
                BorderKey = "SlotReturnedBorderBrush",
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotReturnedBorderBrush"
            },
            "processing" => new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = lotText,
                ShowSpecRow = true,
                StatusText = "处理中",
                BackgroundKey = "SlotOpenBrush",
                BorderKey = "AccentBrush",
                BorderThickness = 2.5,
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "AccentBrush",
                StatusBold = true
            },
            _ => new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = lotText,
                ShowSpecRow = showSpec,
                StatusText = "空闲",
                BackgroundKey = "SlotEmptyBrush",
                BorderKey = "BorderBrush",
                NoForegroundKey = "TextSecondaryBrush",
                StatusForegroundKey = "TextSecondaryBrush"
            }
        };
    }

    private static MhSlotTileModel BuildDisabledTile(
        SlotDoorState state,
        string displayNo,
        string? lotText,
        bool showSpec,
        bool isOpen)
    {
        var hasRetrievableWire = state.BizState is "available_wire" or "returned_wire";
        var statusText = hasRetrievableWire ? "已禁用·可取" : "已禁用";

        return new MhSlotTileModel
        {
            SlotId = state.SlotId,
            SlotNo = state.SlotNo,
            DisplayNo = displayNo,
            TypeText = lotText,
            ShowSpecRow = showSpec && hasRetrievableWire,
            StatusText = isOpen ? $"● {statusText}" : statusText,
            BackgroundKey = "SlotDisabledBrush",
            BorderKey = isOpen ? "SlotOpenBorderBrush" : "SlotDisabledBorderBrush",
            BorderThickness = isOpen ? 2.5 : 1.5,
            NoForegroundKey = "TextPrimaryBrush",
            StatusForegroundKey = isOpen ? "SlotOpenBorderBrush" : "SlotDisabledBorderBrush",
            StatusBold = isOpen
        };
    }

    private static string? WireLotDisplay(SlotDoorState state) =>
        string.IsNullOrWhiteSpace(state.WireLotNo) ? null : state.WireLotNo.Trim();

    private static string WireSpecDisplay(SlotDoorState state) =>
        SlotWireInfoFormatter.FormatSpec(state);

    private static bool ShouldShowLotRow(SlotDoorState state, bool isOpen) =>
        isOpen
        || state.BizState is "available_wire" or "returned_wire" or "processing"
        || WireLotDisplay(state) is not null;

    private static string FormatDisplayNo(string slotNo)
    {
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        return slotNo;
    }
}
