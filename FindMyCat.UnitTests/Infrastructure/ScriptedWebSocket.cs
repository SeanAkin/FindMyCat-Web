using System.Net.WebSockets;
using System.Text;

namespace FindMyCat.UnitTests.Infrastructure;

internal sealed class ScriptedWebSocket : WebSocket
{
    private readonly Queue<Func<ArraySegment<byte>, WebSocketReceiveResult>> _frames = new();
    private WebSocketState _state = WebSocketState.Open;

    public bool CloseSent { get; private set; }

    public ScriptedWebSocket TextFrame(string text, bool endOfMessage = true)
    {
        _frames.Enqueue(buffer =>
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            bytes.CopyTo(buffer.Array!, buffer.Offset);
            return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, endOfMessage);
        });
        return this;
    }

    public ScriptedWebSocket CloseFrame()
    {
        _frames.Enqueue(_ =>
        {
            _state = WebSocketState.CloseReceived;
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, null);
        });
        return this;
    }

    public ScriptedWebSocket ConnectionLost()
    {
        _frames.Enqueue(_ => throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely));
        return this;
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
        Task.FromResult(_frames.Dequeue()(buffer));

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        CloseSent = true;
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public override void Abort() => _state = WebSocketState.Aborted;

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public override void Dispose()
    {
    }
}
