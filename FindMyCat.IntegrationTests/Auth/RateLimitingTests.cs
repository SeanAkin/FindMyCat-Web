using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class RateLimitingTests : IClassFixture<RateLimitingTests.RateLimitedFactory>
{
    private readonly RateLimitedFactory _factory;

    public RateLimitingTests(RateLimitedFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ExceedingTheFixedWindow_ReturnsTooManyRequests()
    {
        using var client = _factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest("nobody@example.com", "Wrong!Pass1"),
                TestContext.Current.CancellationToken);
        }

        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var body = await lastResponse.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("too_many_requests");
    }

    public sealed class RateLimitedFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"findmycat-ratelimit-{Guid.NewGuid():N}.db");

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

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();

            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var path = _dbPath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
