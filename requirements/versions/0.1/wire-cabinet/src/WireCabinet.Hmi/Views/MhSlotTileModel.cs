using WireCabinet.Slots;

namespace WireCabinet.Hmi.Views;

/// <summary>MH 格口卡片展示模型（由 app_slot 状态映射到主题资源键与文案）。</summary>
public sealed class MhSlotTileModel
{
    public bool IsPlaceholder { get; init; }
    public long? SlotId { get; init; }
    public string? SlotNo { get; init; }

    public string DisplayNo { get; init; } = "—";
    public string? TypeText { get; init; }
    /// <summary>是否在格口卡片中显示规格行（有料/退回/开门等）。</summary>
    public bool ShowSpecRow { get; init; }
    public string StatusText { get; init; } = "—";

    public string BackgroundKey { get; init; } = "SlotEmptyBrush";
    public string BorderKey { get; init; } = "BorderBrush";
    public double BorderThickness { get; init; } = 1.5;
    public string NoForegroundKey { get; init; } = "TextSecondaryBrush";
    public string StatusForegroundKey { get; init; } = "TextSecondaryBrush";
    public bool StatusBold { get; init; }

    public static MhSlotTileModel Placeholder() => new() { IsPlaceholder = true };

    public static MhSlotTileModel From(SlotDoorState state)
    {
        var displayNo = FormatDisplayNo(state.SlotNo);
        var spec = WireSpecDisplay(state);
        var showSpec = ShouldShowSpecRow(state);

        if (state.IsOpen)
        {
            return new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = spec,
                ShowSpecRow = showSpec,
                StatusText = "● 仓门已开",
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
                TypeText = spec,
                ShowSpecRow = true,
                StatusText = "待发放",
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
                TypeText = spec,
                ShowSpecRow = true,
                StatusText = "退回",
                BackgroundKey = "SlotReturnedBrush",
                BorderKey = "SlotReturnedBorderBrush",
                NoForegroundKey = "TextPrimaryBrush",
                StatusForegroundKey = "SlotReturnedBorderBrush",
                StatusBold = true
            },
            "processing" => new MhSlotTileModel
            {
                SlotId = state.SlotId,
                SlotNo = state.SlotNo,
                DisplayNo = displayNo,
                TypeText = spec,
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
                TypeText = spec,
                ShowSpecRow = showSpec,
                StatusText = "空闲",
                BackgroundKey = "SlotEmptyBrush",
                BorderKey = "BorderBrush",
                NoForegroundKey = "TextSecondaryBrush",
                StatusForegroundKey = "TextSecondaryBrush"
            }
        };
    }

    private static string? WireSpecDisplay(SlotDoorState state)
    {
        if (!string.IsNullOrWhiteSpace(state.WireSpec))
            return state.WireSpec.Trim();
        if (!string.IsNullOrWhiteSpace(state.WireLotNo))
            return state.WireLotNo.Trim();
        return null;
    }

    private static bool ShouldShowSpecRow(SlotDoorState state) =>
        state.IsOpen
        || state.BizState is "available_wire" or "returned_wire" or "processing"
        || WireSpecDisplay(state) is not null;

    private static string FormatDisplayNo(string slotNo)
    {
        if (slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        if (slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
            return slotNo[2..];
        return slotNo;
    }
}
