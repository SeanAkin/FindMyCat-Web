using FindMyCat.Api.Contracts;
using FindMyCat.Api.Hubs;
using FindMyCat.Core.Integrations.Traccar.Socket;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

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

    [Fact]
    public void Browsers_are_told_live_updates_are_disabled_instead_of_traccar_being_unavailable()
    {
        _factory.Browser.Setup(c => c.TraccarConnectionChanged(It.IsAny<TraccarConnectionStatusResponse>())).Returns(Task.CompletedTask);

        _factory.Services.GetRequiredService<DevicesHubPublisher>().SendCurrentStatusTo("browser");

        _factory.Browser.Verify(c => c.TraccarConnectionChanged(
            TraccarConnectionStatusResponse.FromDomain(TraccarConnectionStatus.LiveUpdatesDisabled)));
    }

    public sealed class LiveUpdatesDisabledFactory : WebApplicationFactory<Program>
    {
        public Mock<IDevicesClient> Browser { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            var clients = new Mock<IHubClients<IDevicesClient>>();
            clients.Setup(c => c.Client("browser")).Returns(Browser.Object);
            var hubContext = new Mock<IHubContext<DevicesHub, IDevicesClient>>();
            hubContext.Setup(h => h.Clients).Returns(clients.Object);

            builder.ConfigureTestServices(services => services.AddSingleton(hubContext.Object));
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
