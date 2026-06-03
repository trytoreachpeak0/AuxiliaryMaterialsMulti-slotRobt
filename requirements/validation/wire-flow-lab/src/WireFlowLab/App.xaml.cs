using System.Windows;
using WireFlowLab.Validation;
using WireFlowLab.Views;

namespace WireFlowLab;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 无界面自测模式：dotnet run -- selftest
        if (e.Args.Any(a => string.Equals(a, "selftest", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var path = SelfTestRunner.Run();
                Console.WriteLine("SELFTEST_OK " + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SELFTEST_FAIL " + ex);
            }
            Shutdown(0);
            return;
        }

        var window = new MainWindow();
        window.Show();
    }
}
