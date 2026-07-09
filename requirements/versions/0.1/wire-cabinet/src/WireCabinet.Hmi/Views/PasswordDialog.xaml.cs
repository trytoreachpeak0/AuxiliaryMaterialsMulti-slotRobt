using System.Windows;
using System.Windows.Input;

namespace WireCabinet.Hmi.Views;

/// <summary>通用密码验证框：标题/提示文案/校验回调均可传参，供维护、物料员等界面门禁共用。</summary>
public partial class PasswordDialog : Window
{
    private readonly Func<string?, bool> _tryUnlock;

    public PasswordDialog(string title, string prompt, Func<string?, bool> tryUnlock)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        _tryUnlock = tryUnlock;
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
        if (_tryUnlock(PasswordBox.Password))
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
