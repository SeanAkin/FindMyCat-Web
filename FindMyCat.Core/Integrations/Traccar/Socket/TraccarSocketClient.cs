using System.Net;
using System.Net.WebSockets;
using FindMyCat.Core.Errors;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public interface ITraccarSocketClient
{
    Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default);
}

public sealed class TraccarSocketClient(
    HttpClient httpClient,
    TraccarSessionStore sessions,
    ILogger<TraccarSocketClient> logger) : ITraccarSocketClient
{
    private const string SessionPath = "api/session";
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    public async Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        var sessionCookie = await GetSessionCookieAsync(token, cancellationToken);

        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Cookie", sessionCookie);
        socket.Options.KeepAliveInterval = KeepAliveInterval;
        socket.Options.KeepAliveTimeout = KeepAliveInterval;

        try
        {
            var typedClientHandlersAndTimeout = httpClient;
            await socket.ConnectAsync(SocketUriFor(httpClient.BaseAddress!), typedClientHandlersAndTimeout, cancellationToken);
            return new TraccarSocketConnection(socket, logger);
        }
        catch (WebSocketException ex)
        {
            socket.Dispose();
            throw new TraccarUnavailableException($"Could not open the Traccar socket: {ex.Message}", ex);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private async Task<string> GetSessionCookieAsync(string token, CancellationToken cancellationToken)
    {
        var cookie = await CreateSessionAsync(token, sessions.Cookie, cancellationToken);
        sessions.Cookie = cookie;
        return cookie;
    }

    internal async Task<string> CreateSessionAsync(string token, string? previousSessionCookie, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{SessionPath}?token={Uri.EscapeDataString(token)}");
        if (previousSessionCookie is not null)
        {
            request.Headers.Add("Cookie", previousSessionCookie);
        }

        using var response = await SendSessionRequestAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TraccarCredentialRejectedException($"Traccar returned status {(int)response.StatusCode} for {SessionPath}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new TraccarUnavailableException($"Traccar returned status {(int)response.StatusCode} for {SessionPath}.");
        }

        var cookies = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
            ? setCookies.Select(header => header.Split(';')[0])
            : [];

        var sessionCookie = string.Join("; ", cookies);
        return sessionCookie.Length > 0
            ? sessionCookie
            : throw new TraccarUnavailableException($"Traccar did not return a session cookie for {SessionPath}.");
    }

    private async Task<HttpResponseMessage> SendSessionRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                return await httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new TraccarUnavailableException($"Traccar session request failed to complete: {ex.Message}", ex);
            }
        }
    }

    internal static Uri SocketUriFor(Uri traccarBaseAddress) =>
        new UriBuilder(new Uri(traccarBaseAddress, "api/socket"))
        {
            Scheme = traccarBaseAddress.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs
        }.Uri;
}
