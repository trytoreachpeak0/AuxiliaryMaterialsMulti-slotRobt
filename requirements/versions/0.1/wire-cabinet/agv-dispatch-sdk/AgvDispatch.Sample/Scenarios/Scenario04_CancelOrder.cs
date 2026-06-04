namespace AgvDispatch.Sample.Scenarios;

/// <summary>场景：取消当前车绑定订单（有 OrderTaskId 时）。</summary>
public static class Scenario04_CancelOrder
{
    public static async Task RunAsync(SampleContext ctx, bool execute)
    {
        Console.WriteLine("=== 取消当前订单 ===");

        var vehicle = await ctx.Client.GetVehicleInfoAsync();
        if (string.IsNullOrWhiteSpace(vehicle.OrderTaskId))
        {
            Console.WriteLine("当前无 OrderTaskId，无需取消。");
            return;
        }

        Console.WriteLine($"OrderTaskId={vehicle.OrderTaskId}");
        if (!execute)
        {
            Console.WriteLine("【干跑】加 --execute 将调用 CancelOrderAsync");
            return;
        }

        await ctx.Client.CancelOrderAsync(vehicle.OrderTaskId);
        Console.WriteLine("已发送取消命令。");
    }
}
