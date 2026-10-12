using FindMyCat.Core.Integrations.Traccar.Socket;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class LiveUpdatesDisabledTests : IClassFixture<LiveUpdatesDisabledTests.LiveUpdatesDisabledFactory>
{
    private readonly LiveUpdatesDisabledFactory _factory;

    public LiveUpdatesDisabledTests(LiveUpdatesDisabledFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Traccar_socket_relay_is_not_started_when_disabled()
    {
        _factory.Services.GetServices<IHostedService>().OfType<TraccarSocketRelay>().ShouldBeEmpty();
    }

    public sealed class LiveUpdatesDisabledFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FINDMYCAT_ENCRYPTION_KEY"] = "gFpnIeo9r9VhDpFCizlLsBa/0ARQH/nMsdfnz1lCQeI=",
                ["ConnectionStrings:Default"] = "Data Source=:memory:",
                ["Traccar:BaseUrl"] = "https://traccar.invalid/",
                ["Traccar:LiveUpdates:Enabled"] = "false"
            }));

            return base.CreateHost(builder);
        }
    }
}
