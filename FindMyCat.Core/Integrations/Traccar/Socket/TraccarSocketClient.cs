using System.Net;
using System.Net.WebSockets;
using FindMyCat.Core.Errors;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public interface ITraccarSocketClient
{
    Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default);
}

public sealed class TraccarSocketClient(HttpClient httpClient, ILogger<TraccarSocketClient> logger) : ITraccarSocketClient
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    public async Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        var sessionCookie = await CreateSessionAsync(token, cancellationToken);

        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Cookie", sessionCookie);
        socket.Options.KeepAliveInterval = KeepAliveInterval;
        socket.Options.KeepAliveTimeout = KeepAliveInterval;

        // ClientWebSocket has no handshake timeout of its own, so apply the typed client's timeout here.
        using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeTimeout.CancelAfter(httpClient.Timeout);

        try
        {
            await socket.ConnectAsync(SocketUriFor(httpClient.BaseAddress!), handshakeTimeout.Token);
            return new TraccarSocketConnection(socket, logger);
        }
        catch (WebSocketException ex)
        {
            socket.Dispose();
            throw new TraccarUnavailableException($"Could not open the Traccar socket: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new TraccarUnavailableException(
                $"Traccar did not complete the socket handshake within {httpClient.Timeout.TotalSeconds:0}s.", ex);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal async Task<string> CreateSessionAsync(string token, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync($"api/session?token={Uri.EscapeDataString(token)}", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new TraccarUnavailableException($"Traccar session request failed to complete: {ex.Message}", ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new TraccarCredentialRejectedException($"Traccar returned status {(int)response.StatusCode} for api/session.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new TraccarUnavailableException($"Traccar returned status {(int)response.StatusCode} for api/session.");
            }

            var cookies = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                ? setCookies.Select(header => header.Split(';')[0])
                : [];

            var sessionCookie = string.Join("; ", cookies);
            return sessionCookie.Length > 0
                ? sessionCookie
                : throw new TraccarUnavailableException("Traccar did not return a session cookie for api/session.");
        }
    }

    internal static Uri SocketUriFor(Uri traccarBaseAddress) =>
        new UriBuilder(new Uri(traccarBaseAddress, "api/socket"))
        {
            Scheme = traccarBaseAddress.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs
        }.Uri;
}
