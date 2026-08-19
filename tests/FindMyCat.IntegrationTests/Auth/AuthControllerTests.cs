using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Session_returns_the_authenticated_users_details()
    {
        var user = await CreateUserAsync(
            UserRole.Administrator,
            email: "cat.owner@example.com",
            displayName: "Cat Owner",
            cancellationToken: TestContext.Current.CancellationToken);

        using var client = CreateAuthenticatedClient(user);

        var session = await client.GetFromJsonAsync<SessionResponse>(
            "/auth/session", TestContext.Current.CancellationToken);

        session!.Email.ShouldBe("cat.owner@example.com");
        session.DisplayName.ShouldBe("Cat Owner");
        session.Role.ShouldBe("Administrator");
    }

    [Fact]
    public async Task Logout_signs_the_user_out()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsync("/auth/logout", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
