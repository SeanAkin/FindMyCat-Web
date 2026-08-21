using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Admin;

public sealed class AdminControllerTests : IntegrationTestBase
{
    public AdminControllerTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Standard_user_cannot_access_admin_endpoints()
    {
        var user = await CreateUserAsync(UserRole.User, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/allowed-emails", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Administrator_can_add_an_allowed_email()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        var addResponse = await client.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest("friend@example.com"),
            TestContext.Current.CancellationToken);

        addResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var added = await addResponse.Content.ReadFromJsonAsync<AllowedEmailResponse>(TestContext.Current.CancellationToken);
        added!.Email.ShouldBe("friend@example.com");
    }

    [Fact]
    public async Task Administrator_can_list_allowed_emails()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        await client.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest("friend@example.com"),
            TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest("cousin@example.com"),
            TestContext.Current.CancellationToken);

        var list = await client.GetFromJsonAsync<List<AllowedEmailResponse>>(
            "/api/admin/allowed-emails", TestContext.Current.CancellationToken) ?? [];

        list.ShouldContain(e => e.Email == "friend@example.com");
        list.ShouldContain(e => e.Email == "cousin@example.com");
    }

    [Fact]
    public async Task Administrator_can_remove_an_allowed_email()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);
        await client.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest("friend@example.com"),
            TestContext.Current.CancellationToken);

        var deleteResponse = await client.DeleteAsync(
            "/api/admin/allowed-emails/friend@example.com", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterDelete = await client.GetFromJsonAsync<List<AllowedEmailResponse>>(
            "/api/admin/allowed-emails", TestContext.Current.CancellationToken) ?? [];
        afterDelete.ShouldNotContain(e => e.Email == "friend@example.com");
    }

    [Fact]
    public async Task Removing_an_allowed_email_also_deletes_the_matching_users_account()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);
        var member = await CreateUserAsync(UserRole.User, email: "friend@example.com", cancellationToken: TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest(member.Email),
            TestContext.Current.CancellationToken);

        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/allowed-emails/{member.Email}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var users = await client.GetFromJsonAsync<List<UserResponse>>(
            "/api/admin/users", JsonOptionsMatchingServerEnumSerialization, TestContext.Current.CancellationToken) ?? [];
        users.ShouldNotContain(u => u.Id == member.Id);
    }

    [Fact]
    public async Task Administrator_cannot_remove_the_primary_administrators_allowed_email()
    {
        var primaryAdmin = await CreateUserAsync(
            UserRole.Administrator,
            email: "founder@example.com",
            isPrimaryAdministrator: true,
            cancellationToken: TestContext.Current.CancellationToken);
        var otherAdmin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(otherAdmin);
        await client.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest(primaryAdmin.Email),
            TestContext.Current.CancellationToken);

        var response = await client.DeleteAsync(
            $"/api/admin/allowed-emails/{primaryAdmin.Email}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<AdminErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("primary_administrator_protected");

        var list = await client.GetFromJsonAsync<List<AllowedEmailResponse>>(
            "/api/admin/allowed-emails", TestContext.Current.CancellationToken) ?? [];
        list.ShouldContain(e => e.Email == primaryAdmin.Email);
    }

    [Fact]
    public async Task Administrator_can_list_users()
    {
        var admin = await CreateUserAsync(
            UserRole.Administrator,
            email: "admin@example.com",
            cancellationToken: TestContext.Current.CancellationToken);
        await CreateUserAsync(
            UserRole.User,
            email: "member@example.com",
            cancellationToken: TestContext.Current.CancellationToken);

        using var client = CreateAuthenticatedClient(admin);

        var users = await client.GetFromJsonAsync<List<UserResponse>>(
            "/api/admin/users", JsonOptionsMatchingServerEnumSerialization, TestContext.Current.CancellationToken) ?? [];

        users.ShouldContain(u => u.Email == "admin@example.com" && u.Role == UserRole.Administrator);
        users.ShouldContain(u => u.Email == "member@example.com" && u.Role == UserRole.User);
    }

    [Fact]
    public async Task Administrator_can_promote_a_standard_user_to_administrator()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);
        var partner = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{partner.Id}/role",
            new UpdateUserRoleRequest(UserRole.Administrator),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var users = await client.GetFromJsonAsync<List<UserResponse>>(
            "/api/admin/users", JsonOptionsMatchingServerEnumSerialization, TestContext.Current.CancellationToken) ?? [];
        users.ShouldContain(u => u.Id == partner.Id && u.Role == UserRole.Administrator);
    }

    [Fact]
    public async Task Administrator_can_demote_a_non_primary_administrator()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);
        var promoted = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{promoted.Id}/role",
            new UpdateUserRoleRequest(UserRole.User),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Primary_administrators_role_cannot_be_changed()
    {
        var primaryAdmin = await CreateUserAsync(
            UserRole.Administrator, isPrimaryAdministrator: true, cancellationToken: TestContext.Current.CancellationToken);
        var otherAdmin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(otherAdmin);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{primaryAdmin.Id}/role",
            new UpdateUserRoleRequest(UserRole.User),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<AdminErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("primary_administrator_protected");
    }

    [Fact]
    public async Task Setting_role_for_unknown_user_returns_not_found()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(admin);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{Guid.NewGuid()}/role",
            new UpdateUserRoleRequest(UserRole.Administrator),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Standard_user_cannot_change_roles()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);
        var other = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{other.Id}/role",
            new UpdateUserRoleRequest(UserRole.Administrator),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
