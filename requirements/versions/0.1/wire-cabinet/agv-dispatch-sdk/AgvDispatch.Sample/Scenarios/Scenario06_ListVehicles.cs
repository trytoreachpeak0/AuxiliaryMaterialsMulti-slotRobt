namespace AgvDispatch.Sample.Scenarios;

public static class Scenario06_ListVehicles
{
    public static async Task RunAsync(SampleContext ctx)
    {
        Console.WriteLine("=== 多车列表 ===");
        var list = await ctx.Client.GetVehiclesAsync();
        foreach (var v in list)
        {
            Console.WriteLine(
                $"  {v.DeviceName ?? v.DeviceKey} | sys={v.SysState} pos={v.CurrentPosition} " +
                $"bat={v.Battery} order={v.OrderTaskId}");
        }
    }
}
