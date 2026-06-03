using CommunityToolkit.Mvvm.ComponentModel;

namespace WireFlowLab.ViewModels;

public partial class InputFieldViewModel : ObservableObject
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public bool Numeric { get; init; }

    [ObservableProperty] private string _value = "";
}
