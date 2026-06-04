namespace WireCabinet.Hmi.Views;

/// <summary>格口网格卡片统一视觉模型（MH / MAINT 共用渲染）。</summary>
public sealed class CabinetSlotTileVisual
{
    public bool IsPlaceholder { get; init; }
    public long? SlotId { get; init; }
    public string? SlotNo { get; init; }

    public string DisplayNo { get; init; } = "—";
    public string? MiddleText { get; init; }
    public bool ShowMiddleRow { get; init; }
    public string StatusText { get; init; } = "—";

    public string BackgroundKey { get; init; } = "SlotEmptyBrush";
    public string BorderKey { get; init; } = "BorderBrush";
    public double BorderThickness { get; init; } = 1.5;
    public string NoForegroundKey { get; init; } = "TextSecondaryBrush";
    public string StatusForegroundKey { get; init; } = "TextSecondaryBrush";
    public bool StatusBold { get; init; }

    public static CabinetSlotTileVisual Placeholder() => new() { IsPlaceholder = true };

    public static CabinetSlotTileVisual FromMh(MhSlotTileModel m) =>
        new()
        {
            IsPlaceholder = m.IsPlaceholder,
            SlotId = m.SlotId,
            SlotNo = m.SlotNo,
            DisplayNo = m.DisplayNo,
            MiddleText = m.TypeText,
            ShowMiddleRow = m.ShowSpecRow,
            StatusText = m.StatusText,
            BackgroundKey = m.BackgroundKey,
            BorderKey = m.BorderKey,
            BorderThickness = m.BorderThickness,
            NoForegroundKey = m.NoForegroundKey,
            StatusForegroundKey = m.StatusForegroundKey,
            StatusBold = m.StatusBold
        };

    public static CabinetSlotTileVisual FromMaint(MaintSlotTileModel m) =>
        new()
        {
            IsPlaceholder = m.IsPlaceholder,
            SlotId = m.SlotId,
            SlotNo = m.SlotNo,
            DisplayNo = m.DisplayNo,
            MiddleText = m.MiddleText,
            ShowMiddleRow = m.ShowMiddleRow,
            StatusText = m.StatusText,
            BackgroundKey = m.BackgroundKey,
            BorderKey = m.BorderKey,
            BorderThickness = m.BorderThickness,
            NoForegroundKey = m.NoForegroundKey,
            StatusForegroundKey = m.StatusForegroundKey,
            StatusBold = m.StatusBold
        };
}
