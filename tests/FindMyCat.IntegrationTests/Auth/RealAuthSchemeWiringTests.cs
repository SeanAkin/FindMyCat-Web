using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class RealAuthSchemeWiringTests : IClassFixture<RealAuthSchemeWiringTests.RealSchemeFactory>
{
    private readonly RealSchemeFactory _factory;

    public RealAuthSchemeWiringTests(RealSchemeFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_request_gets_401_not_a_redirect_to_google()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/session", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Failed_google_callback_redirects_to_the_frontend_login_page_instead_of_returning_raw_json()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/callback", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldStartWith("/login?error=");
    }

    public sealed class RealSchemeFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"findmycat-authscheme-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FINDMYCAT_ENCRYPTION_KEY"] = "gFpnIeo9r9VhDpFCizlLsBa/0ARQH/nMsdfnz1lCQeI=",
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["Authentication:Google:ClientId"] = "test-client-id",
                ["Authentication:Google:ClientSecret"] = "test-client-secret",
                ["Traccar:BaseUrl"] = "https://traccar.invalid/"
            }));

            return base.CreateHost(builder);
        }
    }
}
