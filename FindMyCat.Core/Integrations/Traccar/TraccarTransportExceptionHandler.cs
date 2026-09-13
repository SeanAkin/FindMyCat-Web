using Microsoft.Extensions.Logging;

using FindMyCat.Core.Errors;

namespace FindMyCat.Core.Integrations.Traccar;

public sealed class TraccarTransportExceptionHandler(ILogger<TraccarTransportExceptionHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Traccar request to {Path} failed to complete.", request.RequestUri);
            throw new TraccarUnavailableException($"Traccar request to {request.RequestUri} failed to complete.", ex);
        }
    }
}
