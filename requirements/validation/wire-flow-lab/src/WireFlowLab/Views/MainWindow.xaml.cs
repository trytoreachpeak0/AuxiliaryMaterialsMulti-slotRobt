using System.Windows;
using WireFlowLab.ViewModels;

namespace WireFlowLab.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
