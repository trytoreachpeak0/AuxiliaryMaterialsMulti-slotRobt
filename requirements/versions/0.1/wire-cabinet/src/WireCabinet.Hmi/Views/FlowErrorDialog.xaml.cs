using System.Windows;

namespace WireCabinet.Hmi.Views;

public partial class FlowErrorDialog : Window
{
    private FlowErrorDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = string.IsNullOrWhiteSpace(message) ? "操作未通过。" : message.Trim();
    }

    public static void Show(Window owner, string message)
    {
        var dialog = new FlowErrorDialog(message) { Owner = owner };
        dialog.ShowDialog();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
