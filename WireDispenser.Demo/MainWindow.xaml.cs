using System.Windows;
using System.Windows.Input;
using WireDispenser.Demo.Views;

namespace WireDispenser.Demo;

public partial class MainWindow : Window
{
    private readonly OpView _opView = new();
    private readonly MhView _mhView = new();

    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = _opView;
        StatusText.Text = "当前角色：操作员 OP";
    }

    private void RbOp_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContent == null) return;
        MainContent.Content = _opView;
        StatusText.Text = "当前角色：操作员 OP";
    }

    private void RbMh_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContent == null) return;
        MainContent.Content = _mhView;
        StatusText.Text = "当前角色：物料员 MH";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public void SetStatus(string message)
    {
        if (StatusText != null)
            StatusText.Text = message;
    }
}
