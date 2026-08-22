using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Api.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class GoogleAuthDisabledTests : IClassFixture<GoogleAuthDisabledTests.GoogleDisabledFactory>
{
    private readonly GoogleDisabledFactory _factory;

    public GoogleAuthDisabledTests(GoogleDisabledFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Providers_omits_google_when_disabled()
    {
        using var client = _factory.CreateClient();

        var providers = await client.GetFromJsonAsync<AuthProvidersResponse>(
            "/auth/providers", ApiJsonOptions.Default, TestContext.Current.CancellationToken);

        providers!.Providers.ShouldBe([AuthProvider.Password]);
    }

    [Fact]
    public async Task Login_returns_not_found_instead_of_challenging_google()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/login", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public sealed class GoogleDisabledFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"findmycat-googledisabled-{Guid.NewGuid():N}.db");

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
                ["Authentication:Google:Enabled"] = "false",
                ["Traccar:BaseUrl"] = "https://traccar.invalid/"
            }));

            return base.CreateHost(builder);
        }
    }
}
