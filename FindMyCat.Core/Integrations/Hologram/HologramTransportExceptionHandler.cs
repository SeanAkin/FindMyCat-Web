using Microsoft.Extensions.Logging;

using FindMyCat.Core.Errors;

namespace FindMyCat.Core.Integrations.Hologram;

public sealed class HologramTransportExceptionHandler(ILogger<HologramTransportExceptionHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Hologram request to {Path} failed to complete.", request.RequestUri);
            throw new HologramUnavailableException($"Hologram request to {request.RequestUri} failed to complete.", ex);
        }
    }
}
