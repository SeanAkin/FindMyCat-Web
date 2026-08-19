using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using FindMyCat.Data;
using FindMyCat.IntegrationTests.Stubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FindMyCat.IntegrationTests.Infrastructure;

public sealed class FindMyCatApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"findmycat-tests-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    public StubTraccarClient TraccarClient { get; } = new();
    public StubHologramClient HologramClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(TestAuthDefaults.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthDefaults.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthDefaults.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthDefaults.SchemeName;
                options.DefaultChallengeScheme = TestAuthDefaults.SchemeName;
            });

            services.AddSingleton<ITraccarClient>(TraccarClient);
            services.AddSingleton<IHologramClient>(HologramClient);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new TestOutputLoggerProvider());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(GetTestAppSetting());
        });
        
        var host = base.CreateHost(builder);

        // TestServer starts requests on a fresh ExecutionContext by default, which breaks
        // IntegrationTestBase's AsyncLocal-based output routing (see TestOutputLoggerProvider).
        var server = host.Services.GetRequiredService<IServer>() as TestServer;
        server!.PreserveExecutionContext = true;

        return host;
    }

    public async ValueTask InitializeAsync()
    {
        var migrationOptionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        migrationOptionsBuilder.UseSqlite(ConnectionString);

        await using var migrationContext = new AppDbContext(migrationOptionsBuilder.Options);
        await migrationContext.Database.MigrateAsync();
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

    private Dictionary<string, string?> GetTestAppSetting()
        => new()
        {
            ["FINDMYCAT_ENCRYPTION_KEY"] = "gFpnIeo9r9VhDpFCizlLsBa/0ARQH/nMsdfnz1lCQeI=",
            ["ConnectionStrings:Default"] = ConnectionString,
            ["Authentication:Google:ClientId"] = "test-client-id",
            ["Authentication:Google:ClientSecret"] = "test-client-secret",
            ["Traccar:BaseUrl"] = "https://traccar.invalid/"
        };
}