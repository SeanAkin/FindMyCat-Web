using System.Net;
using System.Text;
using System.Net.Http.Json;
using FindMyCat.Api.Errors;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Errors;

public sealed class ErrorResponseShapeTests : IntegrationTestBase
{
    public ErrorResponseShapeTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Unauthenticated_request_is_described_by_its_status_alone()
    {
        var response = await Client.GetAsync("/auth/session", TestContext.Current.CancellationToken);

        await AssertApiErrorAsync(response, HttpStatusCode.Unauthorized, expectedCode: null);
    }

    [Fact]
    public async Task Forbidden_request_is_described_by_its_status_alone()
    {
        var user = await CreateUserAsync(UserRole.User, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);

        await AssertApiErrorAsync(response, HttpStatusCode.Forbidden, expectedCode: null);
    }

    [Fact]
    public async Task Status_only_failures_omit_the_code_and_errors_members_entirely()
    {
        var response = await Client.GetAsync("/auth/session", TestContext.Current.CancellationToken);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.ShouldContain("\"message\"");
        json.ShouldNotContain("\"code\"");
        json.ShouldNotContain("\"errors\"");
    }

    [Fact]
    public async Task Domain_conflict_carries_the_code_the_client_branches_on()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);

        await AssertApiErrorAsync(response, HttpStatusCode.Conflict, ErrorCodes.TraccarNotConfigured);
    }

    [Fact]
    public async Task Domain_not_found_carries_its_own_code_rather_than_a_bare_404()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.Positions = [];

        var response = await client.GetAsync("/api/devices/1/position", TestContext.Current.CancellationToken);

        await AssertApiErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.DevicePositionNotFound);
    }

    [Fact]
    public async Task Upstream_failure_returns_an_api_error_without_leaking_upstream_wording()
    {
        const string upstreamWording = "traccar exploded at 0xdeadbeef";

        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        Traccar.ThrowInstead = new TraccarUnavailableException(upstreamWording);

        var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);

        var body = await AssertApiErrorAsync(response, HttpStatusCode.BadGateway, ErrorCodes.TraccarUnavailable);
        body.Message.ShouldNotContain(upstreamWording);
    }

    [Fact]
    public async Task Hologram_device_not_found_does_not_leak_the_device_identifier()
    {
        const string imei = "356938035643809";

        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        await ConfigureTraccarTokenAsync();
        await ConfigureHologramKeyAsync();
        Traccar.Devices = [new TraccarDevice(1, "Collar", imei, "online", null, false, LatestPosition: null)];

        var response = await client.PostAsync("/api/devices/1/ping", content: null, TestContext.Current.CancellationToken);

        var body = await AssertApiErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.HologramDeviceNotFound);
        body.Message.ShouldNotContain(imei);
    }

    [Theory]
    [InlineData("{ \"email\": ")]
    [InlineData("""{"email":123,"displayName":"Person","password":"Str0ng!Pass"}""")]
    [InlineData("")]
    public async Task A_request_that_cannot_be_bound_is_answered_with_a_flat_message_and_nothing_else(
        string requestBody)
    {
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/auth/register", content, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldBe("""{"message":"The request could not be read."}""");
    }

    private static async Task<ApiError> AssertApiErrorAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatus, string? expectedCode)
    {
        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Code.ShouldBe(expectedCode);
        body.Message.ShouldNotBeNullOrWhiteSpace();
        return body;
    }
}
