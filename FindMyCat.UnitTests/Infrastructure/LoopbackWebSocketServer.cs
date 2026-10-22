using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace FindMyCat.UnitTests.Infrastructure;

internal sealed class LoopbackWebSocketServer : IAsyncDisposable
{
    private const string Rfc6455AcceptKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TaskCompletionSource<IReadOnlyDictionary<string, string>> _upgradeRequestHeaders =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _accepting;
    private TcpClient? _client;

    public LoopbackWebSocketServer()
    {
        _listener.Start();
        BaseAddress = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _accepting = AcceptUpgradeAsync();
    }

    public Uri BaseAddress { get; }

    public Task<IReadOnlyDictionary<string, string>> UpgradeRequestHeaders => _upgradeRequestHeaders.Task;

    private async Task AcceptUpgradeAsync()
    {
        try
        {
            _client = await _listener.AcceptTcpClientAsync();
            var stream = _client.GetStream();
            var headers = await ReadRequestHeadersAsync(stream);

            var acceptKey = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(headers["Sec-WebSocket-Key"] + Rfc6455AcceptKeyGuid)));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n"));

            _upgradeRequestHeaders.TrySetResult(headers);
        }
        catch (Exception ex)
        {
            _upgradeRequestHeaders.TrySetException(ex);
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadRequestHeadersAsync(NetworkStream stream)
    {
        var received = new StringBuilder();
        var buffer = new byte[1024];
        while (!received.ToString().Contains("\r\n\r\n"))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                throw new IOException("The client closed the connection before finishing the upgrade request.");
            }

            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return received.ToString()
            .Split("\r\n")
            .Skip(1)
            .TakeWhile(line => line.Length > 0)
            .Select(line => line.Split(':', 2))
            .ToDictionary(header => header[0].Trim(), header => header[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _client?.Dispose();
        await _accepting;
    }
}
