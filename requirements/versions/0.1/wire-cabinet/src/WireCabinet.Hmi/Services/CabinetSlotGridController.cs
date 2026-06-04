using WireCabinet.Hmi.Views;
using WireCabinet.Slots;
using WireCabinet.Slots.Hardware;

namespace WireCabinet.Hmi.Services;

/// <summary>格口网格：从 FlowSlots 拉取并按前/后柜筛选 27 格。</summary>
public static class CabinetSlotGridController
{
    public const int SlotsPerSide = 27;
    public const int FrontMaxSlotId = 27;

    public static IReadOnlyList<SlotDoorState> GetSideSlots(IReadOnlyList<SlotDoorState> all, bool showFront) =>
        all
            .Where(s => showFront ? IsFrontSlot(s) : IsRearSlot(s))
            .OrderBy(s => s.SlotId)
            .Take(SlotsPerSide)
            .ToList();

    public static bool IsFrontSlot(SlotDoorState s) => IsFrontSlot(s.SlotId, s.SlotNo);

    public static bool IsRearSlot(SlotDoorState s) => IsRearSlot(s.SlotId, s.SlotNo);

    public static bool IsFrontSlot(long slotId, string slotNo) =>
        slotId <= FrontMaxSlotId ||
        slotNo.StartsWith("F-", StringComparison.OrdinalIgnoreCase);

    public static bool IsRearSlot(long slotId, string slotNo) =>
        slotId > FrontMaxSlotId ||
        slotNo.StartsWith("R-", StringComparison.OrdinalIgnoreCase);

    public static List<MhSlotTileModel> BuildMhTiles(IReadOnlyList<SlotDoorState> sideSlots)
    {
        var tiles = new List<MhSlotTileModel>(SlotsPerSide);
        foreach (var slot in sideSlots)
            tiles.Add(MhSlotTileModel.From(slot));
        while (tiles.Count < SlotsPerSide)
            tiles.Add(MhSlotTileModel.Placeholder());
        return tiles;
    }

    public static List<CabinetSlotTileVisual> BuildMhVisuals(IReadOnlyList<SlotDoorState> sideSlots) =>
        BuildMhTiles(sideSlots).Select(CabinetSlotTileVisual.FromMh).ToList();

    public static List<CabinetSlotTileVisual> BuildMaintVisuals(
        IReadOnlyList<SlotDoorState> sideSlots,
        SlotIoConfig ioConfig,
        IReadOnlyDictionary<string, SlotHardwareSnapshot> hwSnapshots)
    {
        var tiles = new List<MaintSlotTileModel>(SlotsPerSide);
        foreach (var slot in sideSlots)
        {
            hwSnapshots.TryGetValue(slot.SlotNo, out var hw);
            var map = ioConfig.FindMapping(slot.SlotNo);
            tiles.Add(MaintSlotTileModel.From(slot, map, hw));
        }
        while (tiles.Count < SlotsPerSide)
            tiles.Add(MaintSlotTileModel.Placeholder());
        return tiles.Select(CabinetSlotTileVisual.FromMaint).ToList();
    }
}
