using System.Net;
using System.Text;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindMyCat.UnitTests.Services.Hologram;

public class HologramClientTests
{
    private static HologramClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://hologram.test/") };
        return new HologramClient(httpClient, NullLogger<HologramClient>.Instance);
    }

    [Fact]
    public async Task FindDeviceIdByImei_returns_first_matching_device()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ =>
            (HttpStatusCode.OK, """{"success":true,"data":[{"id":555,"name":"unique-1"}]}"""));
        var client = CreateClient(handler);

        var id = await client.FindDeviceIdByImeiAsync("key", "unique-1", TestContext.Current.CancellationToken);

        id.ShouldBe(555);
    }

    [Fact]
    public async Task FindDeviceIdByImei_returns_null_when_no_devices_match()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, """{"success":true,"data":[]}"""));
        var client = CreateClient(handler);

        var id = await client.FindDeviceIdByImeiAsync("key", "no-match", TestContext.Current.CancellationToken);

        id.ShouldBeNull();
    }

    [Fact]
    public async Task FindDeviceIdByImei_sends_basic_auth_with_apikey_username()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, """{"success":true,"data":[]}"""));
        var client = CreateClient(handler);

        await client.FindDeviceIdByImeiAsync("secret-key", "device-imei", TestContext.Current.CancellationToken);

        var request = handler.Requests.Single();
        request.Headers.Authorization!.Scheme.ShouldBe("Basic");
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!));
        decoded.ShouldBe("apikey:secret-key");
    }

    [Fact]
    public async Task SendMessage_posts_udp_command_to_port_12345()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true}""", Encoding.UTF8, "application/json")
            };
        });
        var client = CreateClient(handler);

        await client.SendMessageAsync("key", 555, "lost", TestContext.Current.CancellationToken);

        handler.Requests.Single().Method.ShouldBe(HttpMethod.Post);
        capturedBody.ShouldContain("\"deviceids\":[555]");
        capturedBody.ShouldContain("\"data\":\"lost\"");
        capturedBody.ShouldContain("\"port\":12345");
        capturedBody.ShouldContain("\"protocol\":\"UDP\"");
    }

    [Fact]
    public async Task Forbidden_response_throws_with_credential_rejected()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ =>
            (HttpStatusCode.Forbidden, """{"success":false,"error":"Invalid API Key"}"""));
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<HologramUpstreamException>(() => client.FindDeviceIdByImeiAsync("bad", "name", TestContext.Current.CancellationToken));

        ex.CredentialRejected.ShouldBeTrue();
    }

    [Fact]
    public async Task Application_level_failure_throws_without_credential_rejected()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ =>
            (HttpStatusCode.BadRequest, """{"success":false,"error":"Some device IDs are invalid"}"""));
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<HologramUpstreamException>(() => client.SendMessageAsync("key", 1, "ping", TestContext.Current.CancellationToken));

        ex.CredentialRejected.ShouldBeFalse();
        ex.Message.ShouldBe("Some device IDs are invalid");
    }

    [Fact]
    public async Task Malformed_json_throws_upstream_exception()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, "not json"));
        var client = CreateClient(handler);

        await Should.ThrowAsync<HologramUpstreamException>(() => client.FindDeviceIdByImeiAsync("key", "name", TestContext.Current.CancellationToken));
    }
}
