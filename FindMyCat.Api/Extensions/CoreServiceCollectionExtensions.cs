using FindMyCat.Api.Controllers;
using FindMyCat.Api.Hubs;
using FindMyCat.Api.Json;
using FindMyCat.Core;
using FindMyCat.Core.Integrations.Hologram;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.Core.Security;
using FindMyCat.Core.Services;
using FindMyCat.Data;
using Microsoft.AspNetCore.SignalR;

namespace FindMyCat.Api.Extensions;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddFindMyCatServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFindMyCatCore();
        services.AddFindMyCatData(configuration);
        services.AddCredentialProtection(configuration);
        services.AddIntegrationClients(configuration);
        services.AddLiveUpdates(configuration);
        services.AddSingleton(new FirmwareSettings(configuration["Firmware:Directory"] ?? "/firmware"));

        return services;
    }

    private static void AddCredentialProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var credentialKey = AesGcmCredentialProtector.ParseKey(
            configuration["FINDMYCAT_ENCRYPTION_KEY"] is { } key && !string.IsNullOrWhiteSpace(key)
                ? key
                : throw new ArgumentException("Environment variable 'FINDMYCAT_ENCRYPTION_KEY' is missing or empty."));

        services.AddSingleton<ICredentialProtector>(new AesGcmCredentialProtector(credentialKey));
    }

    private static void AddIntegrationClients(this IServiceCollection services, IConfiguration configuration)
    {
        var traccarBaseUrl = configuration["Traccar:BaseUrl"];
        if (!Uri.TryCreate(traccarBaseUrl, UriKind.Absolute, out var traccarBaseUri))
        {
            throw new InvalidOperationException(
                "Traccar:BaseUrl is missing or not an absolute URL (e.g. https://traccar.example.com).");
        }

        var traccarBaseAddressWithResolvableRelativePaths = new Uri(
            traccarBaseUri.AbsoluteUri.EndsWith('/') ? traccarBaseUri.AbsoluteUri : traccarBaseUri.AbsoluteUri + "/");

        services.AddHttpClient<ITraccarClient, TraccarClient>(client =>
        {
            client.BaseAddress = traccarBaseAddressWithResolvableRelativePaths;
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddHttpMessageHandler<TraccarTransportExceptionHandler>();

        services.AddHttpClient<ITraccarSocketClient, TraccarSocketClient>(client =>
        {
            client.BaseAddress = traccarBaseAddressWithResolvableRelativePaths;
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false });

        services.AddSingleton(new HologramSettings(configuration["Hologram:OrgId"]));
        services.AddHttpClient<IHologramClient, HologramClient>(client =>
        {
            client.BaseAddress = new Uri("https://dashboard.hologram.io/");
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddHttpMessageHandler<HologramTransportExceptionHandler>();
    }

    private static void AddLiveUpdates(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR()
            .AddJsonProtocol(options => ApiJsonOptions.Configure(options.PayloadSerializerOptions));

        var liveUpdatesEnabled = configuration.GetValue("Traccar:LiveUpdates:Enabled", true);

        services.AddSingleton(sp => new DevicesHubPublisher(
            sp.GetRequiredService<IHubContext<DevicesHub, IDevicesClient>>(),
            sp.GetRequiredService<ILogger<DevicesHubPublisher>>(),
            liveUpdatesEnabled ? TraccarConnectionStatus.Unavailable : TraccarConnectionStatus.LiveUpdatesDisabled));
        services.AddSingleton<DevicesHubConnections>();
        services.AddSingleton<TraccarSocketPassthrough>();
        services.AddSingleton<LiveClients>();
        services.AddSingleton<ITraccarUpdatePublisher>(sp => sp.GetRequiredService<LiveClients>());
        services.AddSingleton<IConnectedClients>(sp => sp.GetRequiredService<LiveClients>());

        if (liveUpdatesEnabled)
        {
            services.AddHostedService<TraccarSocketRelay>();
        }
    }
}
