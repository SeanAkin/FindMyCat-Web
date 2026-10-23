using System.Net;
using System.Net.Sockets;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarSocketClientTests
{
    private static TraccarSocketClient CreateClient(HttpMessageHandler handler, Uri baseAddress, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = baseAddress };
        if (timeout is not null)
        {
            httpClient.Timeout = timeout.Value;
        }

        return new TraccarSocketClient(httpClient, NullLogger<TraccarSocketClient>.Instance);
    }

    private static StubHttpMessageHandler TokenCheckRespondingWith(HttpStatusCode status) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent("[]") });

    private static Uri UnusedLoopbackAddress()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return new Uri($"http://127.0.0.1:{port}/");
    }

    [Fact]
    public async Task Connect_sends_the_token_on_the_socket_upgrade_without_creating_a_session()
    {
        await using var traccarSocket = new LoopbackWebSocketServer();
        var tokenCheck = TokenCheckRespondingWith(HttpStatusCode.OK);
        var client = CreateClient(new TokenCheckStubWithRealSocket(tokenCheck), traccarSocket.BaseAddress);

        await using var connection = await client.ConnectAsync("token+with/specials", TestContext.Current.CancellationToken);

        var upgrade = await traccarSocket.ReceivedUpgradeRequest;
        upgrade.Target.ShouldBe("/api/socket?token=token%2Bwith%2Fspecials");
        upgrade.Headers.ShouldNotContainKey("Cookie");
        tokenCheck.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Connect_throws_credential_rejected_when_the_upgrade_fails_and_traccar_refuses_the_token()
    {
        var tokenCheck = TokenCheckRespondingWith(HttpStatusCode.Unauthorized);
        var client = CreateClient(new TokenCheckStubWithRealSocket(tokenCheck), UnusedLoopbackAddress());

        await Should.ThrowAsync<TraccarCredentialRejectedException>(
            () => client.ConnectAsync("revoked-token", TestContext.Current.CancellationToken));

        var request = tokenCheck.Requests.Single();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/devices");
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("revoked-token");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Connect_throws_unavailable_when_the_upgrade_fails_but_the_token_is_not_refused(HttpStatusCode tokenCheckStatus)
    {
        var client = CreateClient(new TokenCheckStubWithRealSocket(TokenCheckRespondingWith(tokenCheckStatus)), UnusedLoopbackAddress());

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.ConnectAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Connect_throws_unavailable_when_the_token_check_cannot_reach_traccar_either()
    {
        var unreachable = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var client = CreateClient(new TokenCheckStubWithRealSocket(unreachable), UnusedLoopbackAddress());

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.ConnectAsync("tok", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Connect_throws_unavailable_when_traccar_never_completes_the_handshake()
    {
        using var acceptsTcpButNeverAnswersUpgrade = new TcpListener(IPAddress.Loopback, 0);
        acceptsTcpButNeverAnswersUpgrade.Start();
        var port = ((IPEndPoint)acceptsTcpButNeverAnswersUpgrade.LocalEndpoint).Port;
        var client = CreateClient(
            new TokenCheckStubWithRealSocket(TokenCheckRespondingWith(HttpStatusCode.OK)),
            new Uri($"http://127.0.0.1:{port}/"),
            TimeSpan.FromMilliseconds(250));

        await Should.ThrowAsync<TraccarUnavailableException>(
            () => client.ConnectAsync("tok", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://traccar.test/", "wss://traccar.test/api/socket?token=tok")]
    [InlineData("http://traccar:8082/", "ws://traccar:8082/api/socket?token=tok")]
    [InlineData("https://example.com/traccar/", "wss://example.com/traccar/api/socket?token=tok")]
    public void SocketUri_follows_the_configured_base_address(string baseAddress, string expected)
    {
        TraccarSocketClient.SocketUriFor(new Uri(baseAddress), "tok").ShouldBe(new Uri(expected));
    }

    private sealed class TokenCheckStubWithRealSocket(StubHttpMessageHandler tokenCheck) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _tokenCheck = new(tokenCheck);
        private readonly HttpMessageInvoker _network = new(new SocketsHttpHandler { UseCookies = false });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath.EndsWith("/api/socket")
                ? _network.SendAsync(request, cancellationToken)
                : _tokenCheck.SendAsync(request, cancellationToken);
    }
}
