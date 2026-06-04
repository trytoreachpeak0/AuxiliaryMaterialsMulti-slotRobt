using AgvDispatch.Sample.Integration;

namespace AgvDispatch.Sample.Scenarios;

/// <summary>场景 G：主循环模板（推荐复制到其他项目）。</summary>
public static class Scenario07_ControlLoop
{
    public static async Task RunAsync(SampleContext ctx, int? autoDispatchDestination, int ticks, bool execute)
    {
        Console.WriteLine("=== 主循环 AgvControlLoop（推荐集成方式）===");
        Console.WriteLine($"ticks={ticks} autoDispatch={(execute ? autoDispatchDestination?.ToString() : "关闭")}");

        var doors = new FixedCloseFlagDoorProvider(ctx.GetSimulatedCloseFlag, ctx.Options.StateThresholds);
        var loop = new AgvControlLoop(ctx.Client, ctx.Options, doors, new ConsoleAgvSink());

        for (var i = 0; i < ticks; i++)
        {
            Console.WriteLine($"--- tick {i + 1}/{ticks} ---");
            try
            {
                var dest = execute ? autoDispatchDestination : null;
                await loop.TickAsync(dest);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"tick 失败: {ex.Message}");
            }

            await Task.Delay(ctx.Options.RecommendedPollIntervalMs);
        }
    }
}
