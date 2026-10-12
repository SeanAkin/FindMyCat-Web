using System.Net;
using System.Net.Sockets;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarSocketClientTests
{
    private static TraccarSocketClient CreateClient(StubHttpMessageHandler handler, Uri? baseAddress = null, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = baseAddress ?? new Uri("https://traccar.test/") };
        if (timeout is not null)
        {
            httpClient.Timeout = timeout.Value;
        }

        return new TraccarSocketClient(httpClient, NullLogger<TraccarSocketClient>.Instance);
    }

    private static StubHttpMessageHandler RespondingWith(HttpStatusCode status, params string[] setCookies) =>
        new(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
            foreach (var cookie in setCookies)
            {
                response.Headers.Add("Set-Cookie", cookie);
            }

            return response;
        });

    [Fact]
    public async Task CreateSession_sends_token_and_returns_session_cookie()
    {
        var handler = RespondingWith(HttpStatusCode.OK, "JSESSIONID=abc123; Path=/; HttpOnly");
        var client = CreateClient(handler);

        var cookie = await client.CreateSessionAsync("token+with/specials", TestContext.Current.CancellationToken);

        cookie.ShouldBe("JSESSIONID=abc123");
        var request = handler.Requests.Single();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/session");
        request.RequestUri.Query.ShouldBe("?token=token%2Bwith%2Fspecials");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CreateSession_throws_credential_rejected_when_traccar_refuses_the_token(HttpStatusCode status)
    {
        var client = CreateClient(RespondingWith(status));

        await Should.ThrowAsync<TraccarCredentialRejectedException>(
            () => client.CreateSessionAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSession_throws_unavailable_on_server_error()
    {
        var client = CreateClient(RespondingWith(HttpStatusCode.BadGateway));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.CreateSessionAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSession_throws_unavailable_when_no_session_cookie_is_returned()
    {
        var client = CreateClient(RespondingWith(HttpStatusCode.OK));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.CreateSessionAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSession_throws_unavailable_when_traccar_is_unreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused")));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.CreateSessionAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Connect_throws_unavailable_when_traccar_never_completes_the_handshake()
    {
        // Accepts the TCP connection but never answers the WebSocket upgrade request.
        using var silentServer = new TcpListener(IPAddress.Loopback, 0);
        silentServer.Start();
        var port = ((IPEndPoint)silentServer.LocalEndpoint).Port;
        var client = CreateClient(
            RespondingWith(HttpStatusCode.OK, "JSESSIONID=abc123"),
            new Uri($"http://127.0.0.1:{port}/"),
            TimeSpan.FromMilliseconds(250));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.ConnectAsync("tok", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://traccar.test/", "wss://traccar.test/api/socket")]
    [InlineData("http://traccar:8082/", "ws://traccar:8082/api/socket")]
    [InlineData("https://example.com/traccar/", "wss://example.com/traccar/api/socket")]
    public void SocketUri_follows_the_configured_base_address(string baseAddress, string expected)
    {
        TraccarSocketClient.SocketUriFor(new Uri(baseAddress)).ShouldBe(new Uri(expected));
    }
}
