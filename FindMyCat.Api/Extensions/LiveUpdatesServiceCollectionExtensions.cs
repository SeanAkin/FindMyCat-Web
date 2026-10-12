using FindMyCat.Api.Hubs;
using FindMyCat.Api.Json;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.Core.Services;

namespace FindMyCat.Api.Extensions;

public static class LiveUpdatesServiceCollectionExtensions
{
    public static IServiceCollection AddFindMyCatLiveUpdates(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR()
            .AddJsonProtocol(options => ApiJsonOptions.Configure(options.PayloadSerializerOptions));

        services.AddSingleton<DevicesHubPublisher>();
        services.AddSingleton<ITraccarUpdatePublisher>(sp => sp.GetRequiredService<DevicesHubPublisher>());
        services.AddSingleton<DevicesHubConnections>();
        services.AddSingleton<IConnectedClients>(sp => sp.GetRequiredService<DevicesHubConnections>());

        if (configuration.GetValue("Traccar:LiveUpdates:Enabled", true))
        {
            services.AddHostedService<TraccarSocketRelay>();
        }

        return services;
    }
}
