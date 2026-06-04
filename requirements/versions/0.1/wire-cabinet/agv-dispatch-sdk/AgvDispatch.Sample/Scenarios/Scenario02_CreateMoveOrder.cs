using AgvDispatch.Sdk.Operations;

namespace AgvDispatch.Sample.Scenarios;

/// <summary>场景 B：评估通过后下移动单。默认仅演示；Sample:AllowExecute=true 或参数 --execute 才真正下单。</summary>
public static class Scenario02_CreateMoveOrder
{
    public static async Task RunAsync(SampleContext ctx, int destination, bool execute)
    {
        Console.WriteLine("=== 场景 B：下移动单 ===");

        if (destination == 0)
        {
            destination = ctx.GetDestinationFromConfig();
            if (destination == 0)
            {
                Console.WriteLine("请配置 Sample:DestinationStationId 或传入: order <站点号> [--execute]");
                return;
            }
        }

        var snapshot = await ctx.Client.GetVehicleSnapshotAsync();
        var doors = DoorStateInput.FromCloseFlag(ctx.GetSimulatedCloseFlag(), ctx.Options.StateThresholds);
        var eval = AgvOperationEvaluator.Evaluate(
            snapshot.Vehicle, snapshot.Order, doors, ctx.Options.StateThresholds);

        Scenario01_PollStatus.PrintVehicle(snapshot.Vehicle, snapshot.Order);
        Console.WriteLine($"目标站点 destination={destination}");
        Console.WriteLine($"CanAcceptOrder={eval.CanAcceptOrder} ({eval.Reason})");

        if (!eval.CanAcceptOrder)
        {
            Console.WriteLine("当前不可下单，已退出。");
            return;
        }

        if (!execute)
        {
            Console.WriteLine("【干跑】未调用 CreateMoveOrderAsync。要真实下单请加 --execute 或 Sample:AllowExecute=true");
            return;
        }

        var created = await ctx.Client.CreateMoveOrderAsync(destination);
        Console.WriteLine($"已下单。RawOrderId={created.OrderId}");
        Console.WriteLine($"响应片段: {Truncate(created.RawResponse, 200)}");
    }

    private static string? Truncate(string? s, int max) =>
        s == null || s.Length <= max ? s : s[..max] + "...";
}
