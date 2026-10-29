using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authentication;

namespace FindMyCat.Api.Hubs;

public sealed class TraccarSocketPassthrough(ILogger<TraccarSocketPassthrough> logger) : ITraccarUpdatePublisher, IConnectedClients
{
    public const string Route = "/api/socket";

    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private sealed record Client(string? UserId, CancellationTokenSource SignedIn);

    private readonly ConcurrentDictionary<WebSocket, Client> _clients = new();

    public async Task AcceptAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var signInExpiresAt = (await context.AuthenticateAsync()).Properties?.ExpiresUtc;
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var signedIn = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        if (signInExpiresAt is { } expiresAt)
        {
            signedIn.CancelAfter(TimeSpan.FromTicks(Math.Max(0, (expiresAt - DateTimeOffset.UtcNow).Ticks)));
        }

        _clients[socket] = new Client(context.User.FindFirstValue(ClaimTypes.NameIdentifier), signedIn);
        try
        {
            await ReadUntilClosedAsync(socket, signedIn.Token);
        }
        catch (OperationCanceledException)
        {
            await CloseAsync(socket);
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            _clients.TryRemove(socket, out _);
        }
    }

    public async Task PublishAsync(TraccarSocketMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Raw.Length == 0 || _clients.IsEmpty)
        {
            return;
        }

        var payload = Encoding.UTF8.GetBytes(message.Raw);
        await Task.WhenAll(_clients.Keys.Select(socket => SendAsync(socket, payload)));
    }

    public Task PublishConnectionChangedAsync(TraccarConnectionStatus status, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void DisconnectUser(Guid userId)
    {
        foreach (var client in _clients.Values.Where(c => c.UserId == userId.ToString()))
        {
            client.SignedIn.Cancel();
        }
    }

    private static async Task CloseAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "signed out", timeout.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            socket.Abort();
        }
    }

    private static async Task ReadUntilClosedAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
            }
        }
    }

    private async Task SendAsync(WebSocket socket, byte[] payload)
    {
        using var timeout = new CancellationTokenSource(SendTimeout);
        try
        {
            await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            logger.LogInformation("Dropping a socket client that could not keep up: {Error}", ex.Message);
            socket.Abort();
        }
    }
}