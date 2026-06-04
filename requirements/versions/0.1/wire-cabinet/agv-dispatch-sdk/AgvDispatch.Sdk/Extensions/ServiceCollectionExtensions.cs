using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgvDispatch.Sdk.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgvDispatchClient(
        this IServiceCollection services,
        Action<AgvDispatchOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<IAgvDispatchClient, AgvDispatchClient>((_, client) =>
        {
            // BaseAddress 在 AgvDispatchClient 内按 Options 设置
        });
        return services;
    }
}
