using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace FindMyCat.UnitTests.Infrastructure;

internal sealed class LoopbackWebSocketServer : IAsyncDisposable
{
    private const string Rfc6455AcceptKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TaskCompletionSource<UpgradeRequest> _upgradeRequest =
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

    public Task<UpgradeRequest> ReceivedUpgradeRequest => _upgradeRequest.Task;

    private async Task AcceptUpgradeAsync()
    {
        try
        {
            _client = await _listener.AcceptTcpClientAsync();
            var stream = _client.GetStream();
            var request = await ReadRequestAsync(stream);

            var acceptKey = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(request.Headers["Sec-WebSocket-Key"] + Rfc6455AcceptKeyGuid)));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n"));

            _upgradeRequest.TrySetResult(request);
        }
        catch (Exception ex)
        {
            _upgradeRequest.TrySetException(ex);
        }
    }

    private static async Task<UpgradeRequest> ReadRequestAsync(NetworkStream stream)
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

        var lines = received.ToString().Split("\r\n");
        var target = lines[0].Split(' ')[1];
        var headers = lines
            .Skip(1)
            .TakeWhile(line => line.Length > 0)
            .Select(line => line.Split(':', 2))
            .ToDictionary(header => header[0].Trim(), header => header[1].Trim(), StringComparer.OrdinalIgnoreCase);

        return new UpgradeRequest(target, headers);
    }

    public sealed record UpgradeRequest(string Target, IReadOnlyDictionary<string, string> Headers);

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _client?.Dispose();
        await _accepting;
    }
}
