using System.Collections.Concurrent;
using System.Threading.Channels;
using FindMyCat.Core.Integrations.Traccar.Socket;

namespace FindMyCat.IntegrationTests.Stubs;

public sealed class StubTraccarSocketClient : ITraccarSocketClient
{
    private Channel<StubTraccarSocketConnection> _connections = Channel.CreateUnbounded<StubTraccarSocketConnection>();
    private StubTraccarSocketConnection? _current;

    public Exception? ThrowInstead { get; set; }
    public ConcurrentQueue<string> ConnectAttempts { get; private set; } = new();

    public void Reset()
    {
        ThrowInstead = null;
        ConnectAttempts = new ConcurrentQueue<string>();
        _connections = Channel.CreateUnbounded<StubTraccarSocketConnection>();
        _current?.Drop();
    }

    public Task<ITraccarSocketConnection> ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        ConnectAttempts.Enqueue(token);

        if (ThrowInstead is not null)
        {
            return Task.FromException<ITraccarSocketConnection>(ThrowInstead);
        }

        var connection = new StubTraccarSocketConnection(token);
        _current = connection;
        _connections.Writer.TryWrite(connection);
        return Task.FromResult<ITraccarSocketConnection>(connection);
    }

    public async Task<StubTraccarSocketConnection> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return await _connections.Reader.ReadAsync(timeout.Token);
    }
}

public sealed class StubTraccarSocketConnection(string token) : ITraccarSocketConnection
{
    private readonly Channel<TraccarSocketMessage> _messages = Channel.CreateUnbounded<TraccarSocketMessage>();
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Token => token;
    public Task Disposed => _disposed.Task;

    public void Send(TraccarSocketMessage message) => _messages.Writer.TryWrite(message);

    public void Drop() => _messages.Writer.TryComplete();

    public IAsyncEnumerable<TraccarSocketMessage> ReadMessagesAsync(CancellationToken cancellationToken = default) =>
        _messages.Reader.ReadAllAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        _messages.Writer.TryComplete();
        _disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
