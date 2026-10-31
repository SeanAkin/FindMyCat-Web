using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Auth;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.IntegrationTests.Ingest;

public sealed class IngestControllerTests : IntegrationTestBase
{
    public IngestControllerTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    private async Task<string> RotateIngestKeyAsync()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.PostAsync("/api/credentials/ingest-key", null, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RotateIngestKeyResponse>(
            JsonOptions, TestContext.Current.CancellationToken);
        return body!.ApiKey;
    }

    private HttpClient CreateIngestClient(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(IngestApiKeyDefaults.HeaderName, apiKey);
        return client;
    }

    [Fact]
    public async Task Status_without_an_api_key_is_unauthorized()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/ingest/status",
            new { id = "collar-1", payload = new { command = "ping", status = "ok" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_with_a_wrong_api_key_is_unauthorized()
    {
        await RotateIngestKeyAsync();
        using var client = CreateIngestClient("not-the-real-key");

        var response = await client.PostAsJsonAsync(
            "/api/ingest/status",
            new { id = "collar-1", payload = new { command = "ping", status = "ok" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_with_a_valid_key_stores_the_event_against_the_unique_id()
    {
        var apiKey = await RotateIngestKeyAsync();
        using var client = CreateIngestClient(apiKey);

        var response = await client.PostAsJsonAsync(
            "/api/ingest/status",
            new { id = "collar-42", payload = new { command = "ping", status = "ok", method = "wifi" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var stored = await Db.DeviceEvents.AsNoTracking()
            .SingleAsync(e => e.UniqueId == "collar-42", TestContext.Current.CancellationToken);
        stored.Kind.ShouldBe("command");
        stored.Payload.ShouldContain("wifi");
    }

    [Fact]
    public async Task Status_rejects_a_payload_with_no_kind_or_command()
    {
        var apiKey = await RotateIngestKeyAsync();
        using var client = CreateIngestClient(apiKey);

        var response = await client.PostAsJsonAsync(
            "/api/ingest/status",
            new { id = "collar-1", payload = new { battery = 80 } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rotating_the_key_invalidates_the_previous_one()
    {
        var firstKey = await RotateIngestKeyAsync();
        var secondKey = await RotateIngestKeyAsync();

        using var oldClient = CreateIngestClient(firstKey);
        var rejected = await oldClient.PostAsJsonAsync(
            "/api/ingest/status",
            new { id = "collar-1", payload = new { command = "ping" } },
            TestContext.Current.CancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var newClient = CreateIngestClient(secondKey);
        var accepted = await newClient.PostAsJsonAsync(
            "/api/ingest/status",
            new { id = "collar-1", payload = new { command = "ping" } },
            TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // Two access points, then battery 87%.
    private const string TwoAccessPointScan = "010210270000aabbcc001122d0010203040506b957";

    [Fact]
    public async Task Wifi_scan_without_an_api_key_is_unauthorized()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/ingest/wifi-scan", new { id = "collar-1", scan = TwoAccessPointScan }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        TraccarPositions.Positions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Wifi_scan_becomes_a_position_and_records_the_ping()
    {
        var apiKey = await RotateIngestKeyAsync();
        using var client = CreateIngestClient(apiKey);

        var response = await client.PostAsJsonAsync(
            "/api/ingest/wifi-scan", new { id = "wifi-collar", scan = TwoAccessPointScan }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TraccarPositions.Positions.ShouldBe([("wifi-collar", Google.Fix, 87)]);
        var ping = await Db.DeviceEvents.AsNoTracking()
            .SingleAsync(e => e.UniqueId == "wifi-collar", TestContext.Current.CancellationToken);
        ping.Kind.ShouldBe("command");
        ping.Payload.ShouldContain("\"status\":\"ok\"");
        ping.Payload.ShouldContain("\"access_points\":2");
        ping.Payload.ShouldContain("\"accuracy_m\":24");
    }

    [Fact]
    public async Task Wifi_scan_google_cannot_place_records_a_failed_ping()
    {
        var apiKey = await RotateIngestKeyAsync();
        using var client = CreateIngestClient(apiKey);
        Google.FailWith = "wifi not known to google";

        var response = await client.PostAsJsonAsync(
            "/api/ingest/wifi-scan", new { id = "unknown-wifi", scan = TwoAccessPointScan }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        TraccarPositions.Positions.ShouldBeEmpty();
        var ping = await Db.DeviceEvents.AsNoTracking()
            .SingleAsync(e => e.UniqueId == "unknown-wifi", TestContext.Current.CancellationToken);
        ping.Payload.ShouldContain("\"status\":\"fail\"");
        ping.Payload.ShouldContain("wifi not known to google");
    }

    [Fact]
    public async Task Wifi_scan_with_one_access_point_is_not_sent_to_google()
    {
        var apiKey = await RotateIngestKeyAsync();
        using var client = CreateIngestClient(apiKey);

        var response = await client.PostAsJsonAsync(
            "/api/ingest/wifi-scan", new { id = "lonely-collar", scan = "010110270000aabbcc001122d057" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        TraccarPositions.Positions.ShouldBeEmpty();
        var ping = await Db.DeviceEvents.AsNoTracking()
            .SingleAsync(e => e.UniqueId == "lonely-collar", TestContext.Current.CancellationToken);
        ping.Payload.ShouldContain("only 1 access point(s)");
    }
}
