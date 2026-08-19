using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.IntegrationTests.Credentials;

public sealed class CredentialsControllerTests : IntegrationTestBase
{
    public CredentialsControllerTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/credentials", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_starts_unconfigured()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var status = await client.GetFromJsonAsync<CredentialStatusResponse>(
            "/api/credentials", TestContext.Current.CancellationToken);

        status!.TraccarConfigured.ShouldBeFalse();
        status.HologramConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task Standard_user_cannot_set_credentials()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync(
            "/api/credentials/traccar",
            new SetTraccarCredentialRequest("some-token"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Standard_user_cannot_delete_credentials()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync("/api/credentials/traccar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_setting_traccar_token_reports_configured_and_stores_it_encrypted()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        const string plaintextToken = "super-secret-traccar-token";

        var putResponse = await client.PutAsJsonAsync(
            "/api/credentials/traccar",
            new SetTraccarCredentialRequest(plaintextToken),
            TestContext.Current.CancellationToken);

        var status = await client.GetFromJsonAsync<CredentialStatusResponse>(
            "/api/credentials", TestContext.Current.CancellationToken);

        var stored = await Db.SharedCredentials.AsNoTracking()
            .SingleAsync(c => c.Id == SharedCredential.SingletonId, TestContext.Current.CancellationToken);

        putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        status!.TraccarConfigured.ShouldBeTrue();
        status.HologramConfigured.ShouldBeFalse();
        stored.TraccarApiTokenProtected.ShouldNotBeNull();
        stored.TraccarApiTokenProtected.ShouldNotContain(plaintextToken);
    }

    [Fact]
    public async Task Both_credentials_can_be_configured_independently()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        await client.PutAsJsonAsync(
            "/api/credentials/traccar",
            new SetTraccarCredentialRequest("traccar-token"),
            TestContext.Current.CancellationToken);
        await client.PutAsJsonAsync(
            "/api/credentials/hologram",
            new SetHologramCredentialRequest("hologram-key"),
            TestContext.Current.CancellationToken);

        var status = await client.GetFromJsonAsync<CredentialStatusResponse>(
            "/api/credentials", TestContext.Current.CancellationToken);

        status!.TraccarConfigured.ShouldBeTrue();
        status.HologramConfigured.ShouldBeTrue();
    }

    [Fact]
    public async Task Deleting_a_configured_credential_returns_no_content_then_reports_unconfigured()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        await client.PutAsJsonAsync(
            "/api/credentials/traccar",
            new SetTraccarCredentialRequest("traccar-token"),
            TestContext.Current.CancellationToken);

        var deleteResponse = await client.DeleteAsync(
            "/api/credentials/traccar", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var status = await client.GetFromJsonAsync<CredentialStatusResponse>(
            "/api/credentials", TestContext.Current.CancellationToken);
        status!.TraccarConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_an_unconfigured_credential_returns_not_found()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        var deleteResponse = await client.DeleteAsync(
            "/api/credentials/hologram", TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Credential_set_by_admin_is_visible_to_every_other_user()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);
        await adminClient.PutAsJsonAsync(
            "/api/credentials/traccar",
            new SetTraccarCredentialRequest("shared-token"),
            TestContext.Current.CancellationToken);

        var familyMember = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var familyClient = CreateAuthenticatedClient(familyMember);

        var status = await familyClient.GetFromJsonAsync<CredentialStatusResponse>(
            "/api/credentials", TestContext.Current.CancellationToken);

        status!.TraccarConfigured.ShouldBeTrue();
    }
}
