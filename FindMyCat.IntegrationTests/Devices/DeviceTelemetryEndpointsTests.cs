using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FindMyCat.Api.Errors;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class DeviceTelemetryEndpointsTests : IntegrationTestBase
{
    private const string UniqueId = "collar-1";

    public DeviceTelemetryEndpointsTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
        Db.DeviceEvents.RemoveRange(Db.DeviceEvents);
        Db.SaveChanges();
    }

    private void HaveDevice() =>
        Traccar.Devices = [new TraccarDevice(1, "Test Collar", UniqueId, "online", null, false, null)];

    private async Task<DeviceEvent> SeedAsync(string kind, string payload)
    {
        var deviceEvent = new DeviceEvent
        {
            UniqueId = UniqueId,
            Kind = kind,
            Payload = payload,
            ReceivedAt = DateTimeOffset.UtcNow
        };
        Db.DeviceEvents.Add(deviceEvent);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return deviceEvent;
    }

    private async Task<JsonElement> GetEventsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task State_is_rejected_when_unauthenticated()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/devices/1/state", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task State_returns_404_when_traccar_does_not_know_the_device()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.Devices = [new TraccarDevice(2, "Other Collar", "collar-2", "online", null, false, null)];

        var response = await client.GetAsync("/api/devices/1/state", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiError>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe(ErrorCodes.DeviceNotFound);
    }

    [Fact]
    public async Task State_returns_the_latest_event_of_each_kind()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();
        await SeedAsync("network", """{"status":"searching"}""");
        await SeedAsync("command", """{"command":"ping","status":"ok"}""");
        await SeedAsync("network", """{"status":"ok","mode":7,"edrx_s":81.92}""");

        var events = await GetEventsAsync(client, "/api/devices/1/state");

        events.GetArrayLength().ShouldBe(2);
        var network = events.EnumerateArray().Single(e => e.GetProperty("kind").GetString() == "network");
        network.GetProperty("data").GetProperty("status").GetString().ShouldBe("ok");
        network.GetProperty("data").GetProperty("mode").GetInt32().ShouldBe(7);
        network.GetProperty("data").GetProperty("edrxSeconds").GetDouble().ShouldBe(81.92);
    }

    [Fact]
    public async Task Events_serialize_the_typed_command_shape()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();
        await SeedAsync("command",
            """{"command":"ping","status":"ok","reason":"user","method":"wifi","satellites":9,"elapsed_s":42,"battery":3.9,"uptime_s":123456}""");

        var events = await GetEventsAsync(client, "/api/devices/1/events");

        var data = events.EnumerateArray().Single().GetProperty("data");
        data.GetProperty("command").GetString().ShouldBe("ping");
        data.GetProperty("method").GetString().ShouldBe("wifi");
        data.GetProperty("satellites").GetInt32().ShouldBe(9);
        data.GetProperty("elapsedSeconds").GetInt32().ShouldBe(42);
        data.GetProperty("battery").GetDouble().ShouldBe(3.9);
        data.GetProperty("uptimeSeconds").GetInt64().ShouldBe(123456L);
    }

    [Fact]
    public async Task Events_return_raw_payload_for_an_unknown_kind()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();
        await SeedAsync("wifi", """{"networks":["home","office"]}""");

        var events = await GetEventsAsync(client, "/api/devices/1/events");

        var single = events.EnumerateArray().Single();
        single.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
        single.GetProperty("raw").GetProperty("networks").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Events_page_with_the_after_cursor()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();
        await SeedAsync("command", """{"command":"ping"}""");
        var cursor = await SeedAsync("command", """{"command":"ping"}""");
        var after = await SeedAsync("command", """{"command":"lost"}""");

        var events = await GetEventsAsync(client, $"/api/devices/1/events?after={cursor.Id}");

        events.GetArrayLength().ShouldBe(1);
        events.EnumerateArray().Single().GetProperty("id").GetInt64().ShouldBe(after.Id);
    }

    [Fact]
    public async Task Events_filter_by_kind()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();
        await SeedAsync("command", """{"command":"ping"}""");
        await SeedAsync("network", """{"status":"ok"}""");

        var events = await GetEventsAsync(client, "/api/devices/1/events?kind=network");

        events.GetArrayLength().ShouldBe(1);
        events.EnumerateArray().Single().GetProperty("kind").GetString().ShouldBe("network");
    }

    [Fact]
    public async Task Events_cap_the_limit()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();

        var response = await client.GetAsync("/api/devices/1/events?limit=101", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Events_reject_a_negative_cursor()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        HaveDevice();

        var response = await client.GetAsync("/api/devices/1/events?after=-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
