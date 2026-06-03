using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WireFlowLab.SlotControl;

namespace WireFlowLab.ViewModels;

public partial class SlotTileViewModel : ObservableObject
{
    private readonly SlotDoorState _state;

    public SlotTileViewModel(SlotDoorState state) => _state = state;

    public long SlotId => _state.SlotId;
    public string SlotNo => _state.SlotNo;
    public string UsageType => _state.UsageType;
    public string BizState => _state.BizState;
    public bool IsOpen => _state.IsOpen;
    public string DoorText => _state.IsOpen ? "门: 开" : "门: 关";
    public string LockText => _state.IsLocked ? "锁: 锁定" : "锁: 解锁";
    public string WireText => string.IsNullOrEmpty(_state.WireLotNo) ? "(空)" : $"{_state.WireLotNo}\n{_state.WireSpec}";

    public Brush DoorBrush => _state.IsOpen
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xCD, 0xD2))   // 浅红：开门
        : new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));  // 浅绿：关门

    public Brush BizBrush => BizState switch
    {
        "available_wire" => new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9)),
        "returned_wire" => new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x82)),
        "processing" => new SolidColorBrush(Color.FromRgb(0xCE, 0x93, 0xD8)),
        _ => new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE))
    };

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(BizState));
        OnPropertyChanged(nameof(DoorText));
        OnPropertyChanged(nameof(LockText));
        OnPropertyChanged(nameof(WireText));
        OnPropertyChanged(nameof(DoorBrush));
        OnPropertyChanged(nameof(BizBrush));
    }
}
