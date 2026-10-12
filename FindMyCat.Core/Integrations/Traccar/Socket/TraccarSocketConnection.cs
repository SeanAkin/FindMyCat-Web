using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FindMyCat.Core.Errors;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public interface ITraccarSocketConnection : IAsyncDisposable
{
    IAsyncEnumerable<TraccarSocketMessage> ReadMessagesAsync(CancellationToken cancellationToken = default);
}

internal sealed class TraccarSocketConnection(WebSocket socket, ILogger logger) : ITraccarSocketConnection
{
    private const int ReceiveBufferSize = 16 * 1024;
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    public async IAsyncEnumerable<TraccarSocketMessage> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var messageBytes = new MemoryStream();

        while (await ReceiveNextMessageAsync(buffer, messageBytes, cancellationToken))
        {
            var message = TryParse(messageBytes);
            if (message is { IsEmpty: false })
            {
                yield return message;
            }
        }
    }

    // Skip a bad message rather than reconnecting: Traccar resends its latest positions on connect,
    // so a message it keeps sending would otherwise cause a reconnect loop.
    private TraccarSocketMessage? TryParse(MemoryStream messageBytes)
    {
        try
        {
            return TraccarSocketMessage.Parse(messageBytes.GetBuffer().AsSpan(0, (int)messageBytes.Length));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Skipped a Traccar socket message of {Length} bytes that could not be parsed.", messageBytes.Length);
            return null;
        }
    }

    private async Task<bool> ReceiveNextMessageAsync(byte[] buffer, MemoryStream messageBytes, CancellationToken cancellationToken)
    {
        messageBytes.SetLength(0);

        try
        {
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (frame.MessageType == WebSocketMessageType.Close)
                {
                    return false;
                }

                messageBytes.Write(buffer, 0, frame.Count);
            } while (!frame.EndOfMessage);

            return true;
        }
        catch (WebSocketException ex)
        {
            throw new TraccarUnavailableException($"The Traccar socket connection was lost: {ex.Message}", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var timeout = new CancellationTokenSource(CloseTimeout);
            try
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
            }
        }

        socket.Dispose();
    }
}
