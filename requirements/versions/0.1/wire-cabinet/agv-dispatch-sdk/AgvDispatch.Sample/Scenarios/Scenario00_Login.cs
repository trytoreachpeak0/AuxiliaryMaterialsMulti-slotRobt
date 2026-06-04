namespace AgvDispatch.Sample.Scenarios;

public static class Scenario00_Login
{
    public static async Task RunAsync(SampleContext ctx)
    {
        Console.WriteLine("=== 登录调度 ===");
        await ctx.Client.LoginAsync();
        Console.WriteLine("LoginAsync 成功（Token 已缓存）。");
    }
}
