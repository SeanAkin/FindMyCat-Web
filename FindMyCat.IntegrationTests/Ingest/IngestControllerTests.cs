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
}
