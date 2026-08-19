using System.Net;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Authorization;

public sealed class DefaultAuthorizationPolicyTests : IntegrationTestBase
{
    public DefaultAuthorizationPolicyTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Protected_endpoint_without_authentication_is_rejected()
    {
        var response = await Client.GetAsync("/auth/session", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AllowAnonymous_login_endpoint_is_reachable_without_authentication()
    {
        var response = await Client.GetAsync("/auth/login", TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }
}
