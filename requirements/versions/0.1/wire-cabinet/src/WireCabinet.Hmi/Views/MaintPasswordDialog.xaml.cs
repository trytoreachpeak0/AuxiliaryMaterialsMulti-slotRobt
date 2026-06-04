using System.Windows;
using System.Windows.Input;
using WireCabinet.Hmi.Services;

namespace WireCabinet.Hmi.Views;

public partial class MaintPasswordDialog : Window
{
    public MaintPasswordDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PasswordBox.Focus();
            ErrorText.Visibility = Visibility.Collapsed;
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => TrySubmit();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TrySubmit();
    }

    private void TrySubmit()
    {
        if (App.MaintAccess.TryUnlock(PasswordBox.Password))
        {
            DialogResult = true;
            Close();
            return;
        }

        ErrorText.Text = "密码错误";
        ErrorText.Visibility = Visibility.Visible;
        PasswordBox.Clear();
        PasswordBox.Focus();
    }
}
