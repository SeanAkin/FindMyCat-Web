using System.Net;
using System.Threading.RateLimiting;
using FindMyCat.Api.Errors;
using Microsoft.AspNetCore.HttpOverrides;

namespace FindMyCat.Api.Extensions;

public static class NetworkingServiceCollectionExtensions
{
    public static IServiceCollection AddFindMyCatForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            TrustAnyUnroutablePrivateNetworkAsReverseProxy(options.KnownIPNetworks);
        });

        return services;
    }

    public static IServiceCollection AddFindMyCatRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 10,
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                await ApiErrorResults.WriteAsync(
                    context.HttpContext.Response, StatusCodes.Status429TooManyRequests,
                    StatusCodeErrors.For(StatusCodes.Status429TooManyRequests), cancellationToken);
            };
        });

        return services;
    }

    private static void TrustAnyUnroutablePrivateNetworkAsReverseProxy(IList<System.Net.IPNetwork> knownNetworks)
    {
        knownNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
        knownNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
        knownNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
    }
}
