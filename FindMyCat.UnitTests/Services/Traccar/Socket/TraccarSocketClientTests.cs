using System.Net;
using System.Net.Sockets;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarSocketClientTests
{
    private readonly TraccarSessionStore _sessions = new();

    private TraccarSocketClient CreateClient(HttpMessageHandler handler, Uri? baseAddress = null, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = baseAddress ?? new Uri("https://traccar.test/") };
        if (timeout is not null)
        {
            httpClient.Timeout = timeout.Value;
        }

        return new TraccarSocketClient(httpClient, _sessions, NullLogger<TraccarSocketClient>.Instance);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, params string[] setCookies)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
        foreach (var cookie in setCookies)
        {
            response.Headers.Add("Set-Cookie", cookie);
        }

        return response;
    }

    private static StubHttpMessageHandler RespondingWith(HttpStatusCode status, params string[] setCookies) =>
        new(_ => Respond(status, setCookies));

    private static Uri UnusedLoopbackAddress()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return new Uri($"http://127.0.0.1:{port}/");
    }

    private static bool IsTokenLogin(HttpRequestMessage request) => request.RequestUri!.Query.Contains("token=");

    private static string? CookieOf(HttpRequestMessage request) =>
        request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : null;

    [Fact]
    public async Task CreateSession_sends_token_and_returns_session_cookie()
    {
        var handler = RespondingWith(HttpStatusCode.OK, "JSESSIONID=abc123; Path=/; HttpOnly");
        var client = CreateClient(handler);

        var cookie = await client.CreateSessionAsync("token+with/specials", null, TestContext.Current.CancellationToken);

        cookie.ShouldBe("JSESSIONID=abc123");
        var request = handler.Requests.Single();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/session");
        request.RequestUri.Query.ShouldBe("?token=token%2Bwith%2Fspecials");
    }

    [Fact]
    public async Task CreateSession_throws_credential_rejected_when_traccar_refuses_the_token()
    {
        var client = CreateClient(RespondingWith(HttpStatusCode.Unauthorized));

        await Should.ThrowAsync<TraccarCredentialRejectedException>(
            () => client.CreateSessionAsync("tok", null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task CreateSession_throws_unavailable_when_traccar_fails(HttpStatusCode status)
    {
        var client = CreateClient(RespondingWith(status));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.CreateSessionAsync("tok", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSession_throws_unavailable_when_no_session_cookie_is_returned()
    {
        var client = CreateClient(RespondingWith(HttpStatusCode.OK));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.CreateSessionAsync("tok", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSession_throws_unavailable_when_traccar_is_unreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused")));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.CreateSessionAsync("tok", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Connect_sends_the_session_cookie_on_the_socket_upgrade()
    {
        await using var traccarSocket = new LoopbackWebSocketServer();
        var client = CreateClient(
            new SessionStubWithRealSocket(RespondingWith(HttpStatusCode.OK, "JSESSIONID=abc123; Path=/")),
            traccarSocket.BaseAddress);

        await using var connection = await client.ConnectAsync("tok", TestContext.Current.CancellationToken);

        var upgradeHeaders = await traccarSocket.UpgradeRequestHeaders;
        upgradeHeaders["Cookie"].ShouldBe("JSESSIONID=abc123");
    }

    [Fact]
    public async Task Connect_throws_unavailable_when_traccar_never_completes_the_handshake()
    {
        using var acceptsTcpButNeverAnswersUpgrade = new TcpListener(IPAddress.Loopback, 0);
        acceptsTcpButNeverAnswersUpgrade.Start();
        var port = ((IPEndPoint)acceptsTcpButNeverAnswersUpgrade.LocalEndpoint).Port;
        var client = CreateClient(
            new SessionStubWithRealSocket(RespondingWith(HttpStatusCode.OK, "JSESSIONID=abc123")),
            new Uri($"http://127.0.0.1:{port}/"),
            TimeSpan.FromMilliseconds(250));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.ConnectAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Every_reconnect_logs_in_with_the_token_and_hands_back_the_previous_session()
    {
        var sessionNumber = 0;
        var session = new StubHttpMessageHandler(_ => Respond(HttpStatusCode.OK, $"JSESSIONID={++sessionNumber}"));
        var client = CreateClient(new SessionStubWithRealSocket(session), UnusedLoopbackAddress());

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Should.ThrowAsync<TraccarUnavailableException>(
                () => client.ConnectAsync("tok", TestContext.Current.CancellationToken));
        }

        session.Requests.ShouldAllBe(r => r.Method == HttpMethod.Get && IsTokenLogin(r));
        session.Requests.Select(CookieOf).ShouldBe([null, "JSESSIONID=1", "JSESSIONID=2"]);
        _sessions.Cookie.ShouldBe("JSESSIONID=3");
    }

    [Fact]
    public async Task A_token_traccar_no_longer_accepts_is_rejected_even_with_a_previous_session()
    {
        _sessions.Cookie = "JSESSIONID=still-signed-in";
        var client = CreateClient(new SessionStubWithRealSocket(RespondingWith(HttpStatusCode.Unauthorized)), UnusedLoopbackAddress());

        await Should.ThrowAsync<TraccarCredentialRejectedException>(
            () => client.ConnectAsync("revoked-token", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://traccar.test/", "wss://traccar.test/api/socket")]
    [InlineData("http://traccar:8082/", "ws://traccar:8082/api/socket")]
    [InlineData("https://example.com/traccar/", "wss://example.com/traccar/api/socket")]
    public void SocketUri_follows_the_configured_base_address(string baseAddress, string expected)
    {
        TraccarSocketClient.SocketUriFor(new Uri(baseAddress)).ShouldBe(new Uri(expected));
    }

    private sealed class SessionStubWithRealSocket(StubHttpMessageHandler session) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _session = new(session);
        private readonly HttpMessageInvoker _network = new(new SocketsHttpHandler { UseCookies = false });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath.EndsWith("/api/socket")
                ? _network.SendAsync(request, cancellationToken)
                : _session.SendAsync(request, cancellationToken);
    }
}
