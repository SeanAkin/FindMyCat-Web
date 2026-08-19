using System.Net;
using FindMyCat.Core.Services.Traccar;
using FindMyCat.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindMyCat.UnitTests.Services.Traccar;

public class TraccarClientTests
{
    private const string DevicesJson = """
        [
          {"id":1,"name":"Test Device One","uniqueId":"unique-1","status":"online","lastUpdate":"2025-01-02T03:04:05.000+00:00","disabled":false,"attributes":{}},
          {"id":2,"name":"Test Device Two","uniqueId":"unique-2","status":"offline","lastUpdate":null,"disabled":true,"attributes":{}}
        ]
        """;

    private const string PositionsJson = """
        [
          {"id":100,"attributes":{"sat":7.0,"batteryLevel":55.0,"motion":false},"deviceId":1,"protocol":"osmand","serverTime":"2025-01-02T03:04:05.000+00:00","deviceTime":"2025-01-02T03:04:05.000+00:00","fixTime":"2025-01-02T03:04:05.000+00:00","valid":true,"latitude":12.34,"longitude":56.78,"altitude":0.0,"speed":0.0,"course":0.0,"accuracy":8.0}
        ]
        """;

    private static TraccarClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://traccar.test/") };
        return new TraccarClient(httpClient, NullLogger<TraccarClient>.Instance);
    }

    [Fact]
    public async Task GetDevicesWithPositions_maps_devices_and_merges_latest_position()
    {
        var handler = StubHttpMessageHandler.ReturningJson(path => path switch
        {
            "/api/devices" => (HttpStatusCode.OK, DevicesJson),
            "/api/positions" => (HttpStatusCode.OK, PositionsJson),
            _ => (HttpStatusCode.NotFound, "[]")
        });
        var client = CreateClient(handler);

        var devices = await client.GetDevicesWithPositionsAsync("tok");

        devices.Count.ShouldBe(2);

        var withPosition = devices.Single(d => d.Id == 1);
        withPosition.Name.ShouldBe("Test Device One");
        withPosition.UniqueId.ShouldBe("unique-1");
        withPosition.Status.ShouldBe("online");
        withPosition.LatestPosition.ShouldNotBeNull();
        withPosition.LatestPosition.Latitude.ShouldBe(12.34);
        withPosition.LatestPosition.BatteryLevel.ShouldBe(55.0);
        withPosition.LatestPosition.Satellites.ShouldBe(7.0);

        var withoutPosition = devices.Single(d => d.Id == 2);
        withoutPosition.Disabled.ShouldBeTrue();
        withoutPosition.LastUpdate.ShouldBeNull();
        withoutPosition.LatestPosition.ShouldBeNull();
    }

    [Fact]
    public async Task GetDevicesWithPositions_sends_bearer_token()
    {
        var handler = StubHttpMessageHandler.ReturningJson(path => path switch
        {
            "/api/devices" => (HttpStatusCode.OK, "[]"),
            "/api/positions" => (HttpStatusCode.OK, "[]"),
            _ => (HttpStatusCode.NotFound, "[]")
        });
        var client = CreateClient(handler);

        await client.GetDevicesWithPositionsAsync("secret-token");

        handler.Requests.ShouldAllBe(r =>
            r.Headers.Authorization!.Scheme == "Bearer" && r.Headers.Authorization.Parameter == "secret-token");
    }

    [Fact]
    public async Task GetPositions_latest_omits_from_and_to_from_query()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, PositionsJson));
        var client = CreateClient(handler);

        var positions = await client.GetPositionsAsync("tok", deviceId: 1, from: null, to: null);

        positions.Count.ShouldBe(1);
        positions[0].DeviceId.ShouldBe(1);
        var query = handler.Requests.Single().RequestUri!.Query;
        query.ShouldContain("deviceId=1");
        query.ShouldNotContain("from=");
        query.ShouldNotContain("to=");
    }

    [Fact]
    public async Task GetPositions_history_includes_from_and_to_in_query()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, "[]"));
        var client = CreateClient(handler);

        var from = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2025, 1, 3, 0, 0, 0, TimeSpan.Zero);
        await client.GetPositionsAsync("tok", deviceId: 1, from, to);

        var query = handler.Requests.Single().RequestUri!.Query;
        query.ShouldContain("deviceId=1");
        query.ShouldContain("from=");
        query.ShouldContain("to=");
    }

    [Fact]
    public async Task Unauthorized_response_throws_with_credential_rejected()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.Unauthorized, "denied"));
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<TraccarUpstreamException>(
            () => client.GetDevicesWithPositionsAsync("tok"));

        ex.CredentialRejected.ShouldBeTrue();
        ex.Message.ShouldNotContain("denied");
    }

    [Fact]
    public async Task Server_error_throws_without_credential_rejected()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.InternalServerError, "error"));
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<TraccarUpstreamException>(
            () => client.GetDevicesWithPositionsAsync("tok"));

        ex.CredentialRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task Malformed_json_throws_upstream_exception()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, "not json"));
        var client = CreateClient(handler);

        await Should.ThrowAsync<TraccarUpstreamException>(() => client.GetDevicesWithPositionsAsync("tok"));
    }
}
