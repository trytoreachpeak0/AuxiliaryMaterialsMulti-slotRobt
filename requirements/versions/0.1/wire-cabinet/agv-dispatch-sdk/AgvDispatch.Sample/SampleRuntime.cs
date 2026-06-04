using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgvDispatch.Sample;

/// <summary>
/// 在其他 .NET 8 项目中可复制：Host + DI 注册 AgvDispatch。
/// </summary>
public static class SampleRuntime
{
    public static Task<SampleContext> CreateAsync(string[]? args = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.SetBasePath(AppContext.BaseDirectory);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables();

        builder.Services.AddAgvDispatchClient(o =>
            builder.Configuration.GetSection(AgvDispatchOptions.SectionName).Bind(o));

        var host = builder.Build();
        return Task.FromResult(new SampleContext(
            host,
            host.Services.GetRequiredService<IAgvDispatchClient>(),
            host.Services.GetRequiredService<IOptions<AgvDispatchOptions>>().Value,
            host.Services.GetRequiredService<IConfiguration>()));
    }
}

public sealed class SampleContext : IAsyncDisposable
{
    public IHost Host { get; }
    public IAgvDispatchClient Client { get; }
    public AgvDispatchOptions Options { get; }
    public IConfiguration Configuration { get; }

    public SampleContext(IHost host, IAgvDispatchClient client, AgvDispatchOptions options, IConfiguration configuration)
    {
        Host = host;
        Client = client;
        Options = options;
        Configuration = configuration;
    }

    public int GetDestinationFromConfig() =>
        Configuration.GetValue("Sample:DestinationStationId", 0);

    public string GetSimulatedCloseFlag() =>
        Configuration.GetValue<string>("Sample:SimulatedCloseFlag")
        ?? new string(Options.StateThresholds.DoorClosedChar, Options.StateThresholds.ExpectedDoorCount);

    public bool IsExecuteEnabled() =>
        Configuration.GetValue("Sample:AllowExecute", false);

    public async ValueTask DisposeAsync()
    {
        if (Host is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else
            Host.Dispose();
    }
}
