using AgvDispatch.Sdk.Operations;

namespace AgvDispatch.Sample.Scenarios;

/// <summary>场景 C：模拟门开→应暂停；门关→应继续（需车在非 IDLE 时门开才暂停，此处用模拟 closeFlag 演示评估逻辑）。</summary>
public static class Scenario03_DoorPauseContinue
{
    public static Task RunAsync(SampleContext ctx, bool execute)
    {
        Console.WriteLine("=== 场景 C：门开暂停 / 门关继续（评估演示）===");

        var t = ctx.Options.StateThresholds;
        var vehicle = new Sdk.Models.VehicleInfoDto
        {
            Enable = true,
            SysState = "EXECUTING",
            ActionState = "AT_RUNNING",
            ProcState = "BUSY"
        };

        var doorOpen = DoorStateInput.FromCloseFlag(
            "0" + new string(t.DoorClosedChar, t.ExpectedDoorCount - 1), t);
        var evalOpen = AgvOperationEvaluator.Evaluate(vehicle, null, doorOpen, t);
        Console.WriteLine($"门开: ShouldPause={evalOpen.ShouldPauseForDoor} Action={evalOpen.RecommendedAction}");

        var doorClosed = DoorStateInput.FromCloseFlag(new string(t.DoorClosedChar, t.ExpectedDoorCount), t);
        var evalClosed = AgvOperationEvaluator.Evaluate(vehicle, null, doorClosed, t, previouslyPausedForDoor: true);
        Console.WriteLine($"门关(曾暂停): ShouldContinue={evalClosed.ShouldContinueAfterDoorClosed} Action={evalClosed.RecommendedAction}");

        if (!execute)
        {
            Console.WriteLine("【干跑】未调用 Pause/Continue API。真实环境请在 AgvControlLoop.TickAsync 中自动调用。");
            return Task.CompletedTask;
        }

        return RunLiveAsync(ctx);
    }

    private static async Task RunLiveAsync(SampleContext ctx)
    {
        try
        {
            var snap = await ctx.Client.GetVehicleSnapshotAsync();
            if (snap.Vehicle.SysState == "IDLE")
                Console.WriteLine("提示: 当前车为 IDLE，现场逻辑通常在非空闲且门开时才 Pause。");

            await ctx.Client.PauseMovementAsync();
            Console.WriteLine("已调用 PauseMovementAsync，5 秒后 Continue...");
            await Task.Delay(5000);
            await ctx.Client.ContinueMovementAsync();
            Console.WriteLine("已调用 ContinueMovementAsync");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"调度调用失败: {ex.Message}");
        }
    }
}
