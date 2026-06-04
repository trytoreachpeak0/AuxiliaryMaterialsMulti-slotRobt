using AgvDispatch.Sdk.Operations;

namespace AgvDispatch.Sample.Scenarios;

/// <summary>场景 F：检查充电桩占用后下充电单。</summary>
public static class Scenario05_Charge
{
    public static async Task RunAsync(SampleContext ctx, bool execute)
    {
        Console.WriteLine("=== 充电任务 ===");

        var list = await ctx.Client.GetVehiclesAsync();
        var occupied = AgvOperationEvaluator.IsChargeStationOccupied(list, ctx.Options);
        Console.WriteLine($"充电桩占用={occupied} 车辆数={list.Count}");

        if (occupied)
        {
            Console.WriteLine("充电桩占用，不下充电单。");
            return;
        }

        if (!execute)
        {
            Console.WriteLine($"【干跑】将调用 CreateChargeOrderAsync → 站点 {ctx.Options.ChargeDestination}");
            return;
        }

        var created = await ctx.Client.CreateChargeOrderAsync();
        Console.WriteLine($"已下充电单。OrderId={created.OrderId}");
    }
}
