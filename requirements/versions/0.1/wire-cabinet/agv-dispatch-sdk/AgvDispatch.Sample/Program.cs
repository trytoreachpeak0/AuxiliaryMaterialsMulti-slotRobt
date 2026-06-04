using AgvDispatch.Sample;
using AgvDispatch.Sample.Scenarios;

static void PrintHelp()
{
    Console.WriteLine("""
        AgvDispatch.Sample — 用法（在你的 .NET 8 项目中可复制 Integration/AgvControlLoop.cs）

          dotnet run -- help
          dotnet run -- poll              场景A：轮询+评估
          dotnet run -- login             仅登录
          dotnet run -- list              多车列表
          dotnet run -- order <站点号>    场景B：下单（默认干跑）
          dotnet run -- pause             场景C：暂停/继续（干跑=仅评估演示）
          dotnet run -- cancel            取消当前订单
          dotnet run -- charge            充电
          dotnet run -- loop [站点号]     主循环，可选自动下单站点
          dotnet run -- all               依次执行 poll/list（不调写接口）

        真实写调度 API 请加:  --execute
        或 appsettings.json: "Sample": { "AllowExecute": true }

        集成到其他项目:
          1. ProjectReference → AgvDispatch.Sdk
          2. 复制 SampleRuntime 的 DI 注册方式
          3. 复制 Integration/AgvControlLoop.cs + IDoorStateProvider 实现
        """);
}

var argsList = args.Length == 0 ? new[] { "help" } : args;
var cmd = argsList[0].ToLowerInvariant();
var execute = argsList.Contains("--execute", StringComparer.OrdinalIgnoreCase)
              || argsList.Contains("-x", StringComparer.OrdinalIgnoreCase);

await using var ctx = await SampleRuntime.CreateAsync(args);

try
{
    switch (cmd)
    {
        case "help":
        case "-h":
        case "?":
            PrintHelp();
            break;

        case "poll":
        case "a":
            await Scenario01_PollStatus.RunAsync(ctx);
            break;

        case "login":
            await Scenario00_Login.RunAsync(ctx);
            break;

        case "list":
            await Scenario06_ListVehicles.RunAsync(ctx);
            break;

        case "order":
        case "b":
            var dest = argsList.Length > 1 && int.TryParse(argsList[1], out var d) ? d : 0;
            await Scenario02_CreateMoveOrder.RunAsync(ctx, dest, execute || ctx.IsExecuteEnabled());
            break;

        case "pause":
        case "c":
            await Scenario03_DoorPauseContinue.RunAsync(ctx, execute || ctx.IsExecuteEnabled());
            break;

        case "cancel":
            await Scenario04_CancelOrder.RunAsync(ctx, execute || ctx.IsExecuteEnabled());
            break;

        case "charge":
        case "f":
            await Scenario05_Charge.RunAsync(ctx, execute || ctx.IsExecuteEnabled());
            break;

        case "loop":
        case "g":
            int? loopDest = null;
            var ticks = 5;
            foreach (var a in argsList.Skip(1))
            {
                if (a is "--execute" or "-x") continue;
                if (int.TryParse(a, out var n))
                {
                    if (loopDest == null) loopDest = n;
                    else ticks = n;
                }
            }
            if (loopDest == null && ctx.GetDestinationFromConfig() > 0)
                loopDest = ctx.GetDestinationFromConfig();
            await Scenario07_ControlLoop.RunAsync(
                ctx, loopDest, ticks, execute || ctx.IsExecuteEnabled());
            break;

        case "all":
            await Scenario00_Login.RunAsync(ctx);
            await Scenario01_PollStatus.RunAsync(ctx);
            await Scenario06_ListVehicles.RunAsync(ctx);
            await Scenario03_DoorPauseContinue.RunAsync(ctx, execute: false);
            Console.WriteLine("all 完成（未执行写操作）。");
            break;

        default:
            Console.WriteLine($"未知命令: {cmd}");
            PrintHelp();
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"  {ex.InnerException.Message}");
    Console.WriteLine("请检查 appsettings.json 中 AgvDispatch 配置与网络。");
    Environment.ExitCode = 1;
}
