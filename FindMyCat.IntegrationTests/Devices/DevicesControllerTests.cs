using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class DevicesControllerTests : IntegrationTestBase
{
    public DevicesControllerTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Devices_returns_409_when_traccar_not_configured()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("traccar_not_configured");
    }

    [Fact]
    public async Task Devices_returns_mapped_devices_with_positions()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();

        var lastUpdate = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var position = new TraccarPosition(1, lastUpdate, lastUpdate, lastUpdate, 12.34, 56.78, 0, 0, 0, 8, true, 55, 7);
        Traccar.Devices =
        [
            new TraccarDevice(1, "Test Device One", "unique-1", "online", lastUpdate, false, position),
            new TraccarDevice(2, "Test Device Two", "unique-2", "offline", null, true, LatestPosition: null)
        ];

        var devices = await client.GetFromJsonAsync<List<DeviceResponse>>("/api/devices", TestContext.Current.CancellationToken);

        devices!.Count.ShouldBe(2);
        var withPosition = devices.Single(d => d.Id == 1);
        withPosition.Name.ShouldBe("Test Device One");
        withPosition.Position.ShouldNotBeNull();
        withPosition.Position.Latitude.ShouldBe(12.34);
        withPosition.Position.BatteryLevel.ShouldBe(55);
        devices.Single(d => d.Id == 2).Position.ShouldBeNull();
    }

    [Fact]
    public async Task Devices_returns_409_when_traccar_rejects_credential()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.ThrowInstead = new TraccarUpstreamException("rejected", credentialRejected: true);

        var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("traccar_credential_rejected");
    }

    [Fact]
    public async Task Devices_returns_502_when_traccar_unavailable()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.ThrowInstead = new TraccarUpstreamException("boom", credentialRejected: false);

        var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("traccar_unavailable");
    }

    [Fact]
    public async Task Position_returns_404_when_device_has_no_position()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.Positions = [];

        var response = await client.GetAsync("/api/devices/1/position", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Position_returns_latest_fix()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        var now = DateTimeOffset.UtcNow;
        Traccar.Positions = [new TraccarPosition(1, now, now, now, 12.34, 56.78, 0, 0, 0, 5, true, 55, 7)];

        var position = await client.GetFromJsonAsync<PositionResponse>("/api/devices/1/position", TestContext.Current.CancellationToken);

        position!.DeviceId.ShouldBe(1);
        position.Latitude.ShouldBe(12.34);
        position.BatteryLevel.ShouldBe(55);
    }

    [Fact]
    public async Task History_rejects_inverted_range()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync(
            "/api/devices/1/history?from=2025-01-03T00:00:00Z&to=2025-01-01T00:00:00Z",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("invalid_range");
    }

    [Fact]
    public async Task History_rejects_range_over_limit()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync(
            "/api/devices/1/history?from=2025-01-01T00:00:00Z&to=2025-06-01T00:00:00Z",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("range_too_large");
    }

    [Fact]
    public async Task History_returns_positions_for_valid_range()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        var t = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Traccar.Positions =
        [
            new TraccarPosition(1, t, t, t, 12.34, 56.78, 0, 0, 0, 5, true, 55, 7),
            new TraccarPosition(1, t.AddMinutes(1), t.AddMinutes(1), t.AddMinutes(1), 12.35, 56.79, 0, 0, 0, 5, true, 54, 6)
        ];

        var history = await client.GetFromJsonAsync<List<PositionResponse>>(
            "/api/devices/1/history?from=2025-01-01T00:00:00Z&to=2025-01-02T00:00:00Z",
            TestContext.Current.CancellationToken);

        history!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Ping_returns_409_when_hologram_not_configured()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.Devices = [new TraccarDevice(1, "Test Collar", "unique-1", "online", null, false, null)];

        var response = await client.PostAsync("/api/devices/1/ping", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("hologram_not_configured");
    }

    [Fact]
    public async Task Ping_sends_command_and_returns_no_content()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        await ConfigureHologramKeyAsync();
        Traccar.Devices = [new TraccarDevice(1, "Test Collar", "unique-1", "online", null, false, null)];
        Hologram.DeviceIdsByImei["unique-1"] = 555;

        var response = await client.PostAsync("/api/devices/1/ping", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Hologram.SentMessages.ShouldContain((555, "ping"));
    }

    [Fact]
    public async Task Lost_returns_404_when_device_not_registered_in_hologram()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        await ConfigureHologramKeyAsync();
        Traccar.Devices = [new TraccarDevice(1, "Test Collar", "unique-1", "online", null, false, null)];

        var response = await client.PostAsync("/api/devices/1/lost", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("hologram_device_not_found");
    }

    [Fact]
    public async Task Active_returns_409_when_hologram_rejects_credential()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        await ConfigureHologramKeyAsync();
        Traccar.Devices = [new TraccarDevice(1, "Test Collar", "unique-1", "online", null, false, null)];
        Hologram.ThrowInstead = new HologramUpstreamException("rejected", credentialRejected: true);

        var response = await client.PostAsync("/api/devices/1/active", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("hologram_credential_rejected");
    }

    [Fact]
    public async Task Active_returns_502_when_hologram_unavailable()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        await ConfigureHologramKeyAsync();
        Traccar.Devices = [new TraccarDevice(1, "Test Collar", "unique-1", "online", null, false, null)];
        Hologram.ThrowInstead = new HologramUpstreamException("boom", credentialRejected: false);

        var response = await client.PostAsync("/api/devices/1/active", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadFromJsonAsync<TraccarErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("hologram_unavailable");
    }

    private async Task ConfigureTraccarTokenAsync()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.PutAsJsonAsync(
            "/api/credentials/traccar", new SetTraccarCredentialRequest("stored-token"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task ConfigureHologramKeyAsync()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.PutAsJsonAsync(
            "/api/credentials/hologram", new SetHologramCredentialRequest("stored-key"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
