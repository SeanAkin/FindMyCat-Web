using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using FindMyCat.Core.Errors;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public interface ITraccarSocketClient
{
    Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default);
}

// Logging in through api/session creates a Traccar session that never expires unless web.sessionTimeout
// is set, so the token goes straight on the socket instead (Traccar 6.2+), which creates no session.
public sealed class TraccarSocketClient(HttpClient httpClient, ILogger<TraccarSocketClient> logger) : ITraccarSocketClient
{
    private const string SocketPath = "api/socket";
    private const string TokenCheckPath = "api/devices";
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    public async Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = KeepAliveInterval;
        socket.Options.KeepAliveTimeout = KeepAliveInterval;

        try
        {
            await socket.ConnectAsync(SocketUriFor(httpClient.BaseAddress!, token), httpClient, cancellationToken);
            return new TraccarSocketConnection(socket, logger);
        }
        catch (WebSocketException ex)
        {
            socket.Dispose();
            if (await TokenIsRejectedAsync(token, cancellationToken))
            {
                throw new TraccarCredentialRejectedException($"Traccar returned status {(int)HttpStatusCode.Unauthorized} for {TokenCheckPath}.");
            }

            throw new TraccarUnavailableException($"Could not open the Traccar socket: {ex.Message}", ex);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Traccar fails the upgrade with a server error for a bad token, so a failed upgrade alone
    // can't tell a rejected token from Traccar being down.
    private async Task<bool> TokenIsRejectedAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, TokenCheckPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.StatusCode == HttpStatusCode.Unauthorized;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    internal static Uri SocketUriFor(Uri traccarBaseAddress, string token) =>
        new UriBuilder(new Uri(traccarBaseAddress, SocketPath))
        {
            Scheme = traccarBaseAddress.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs,
            Query = $"token={Uri.EscapeDataString(token)}"
        }.Uri;
}
