using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services.Hologram;

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
            throw new HologramUpstreamException("Hologram could not be reached.", credentialRejected: false, ex);
        }
    }
}
