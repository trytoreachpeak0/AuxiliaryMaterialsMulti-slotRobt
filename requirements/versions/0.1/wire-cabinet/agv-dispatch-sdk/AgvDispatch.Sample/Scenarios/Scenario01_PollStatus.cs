using AgvDispatch.Sdk.Operations;

namespace AgvDispatch.Sample.Scenarios;

/// <summary>场景 A：轮询单车状态 + 评估是否可接单（不下单）。</summary>
public static class Scenario01_PollStatus
{
    public static async Task RunAsync(SampleContext ctx)
    {
        Console.WriteLine("=== 场景 A：轮询与可接单评估 ===");

        var snapshot = await ctx.Client.GetVehicleSnapshotAsync();
        var doors = DoorStateInput.FromCloseFlag(ctx.GetSimulatedCloseFlag(), ctx.Options.StateThresholds);
        var eval = AgvOperationEvaluator.Evaluate(
            snapshot.Vehicle, snapshot.Order, doors, ctx.Options.StateThresholds);

        PrintVehicle(snapshot.Vehicle, snapshot.Order);
        Console.WriteLine($"门状态(模拟): {(doors.AllDoorsClosed ? "全关" : "未全关")}");
        Console.WriteLine($"CanAcceptOrder={eval.CanAcceptOrder}");
        Console.WriteLine($"RecommendedAction={eval.RecommendedAction}");
        Console.WriteLine($"Reason: {eval.Reason}");
    }

    internal static void PrintVehicle(Sdk.Models.VehicleInfoDto v, Sdk.Models.OrderDetailDto? order)
    {
        Console.WriteLine($"  deviceKey={v.DeviceKey} online={v.Enable}");
        Console.WriteLine($"  sysState={v.SysState} move={v.EffectiveMoveState} proc={v.ProcState}");
        Console.WriteLine($"  station={v.CurrentPosition} battery={v.Battery} orderTaskId={v.OrderTaskId}");
        if (order != null)
            Console.WriteLine($"  order_state={order.EffectiveOrderState}");
    }
}
