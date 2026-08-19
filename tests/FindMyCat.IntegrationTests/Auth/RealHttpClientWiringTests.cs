using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class RealHttpClientWiringTests : IClassFixture<RealAuthSchemeWiringTests.RealSchemeFactory>
{
    private readonly RealAuthSchemeWiringTests.RealSchemeFactory _factory;

    public RealHttpClientWiringTests(RealAuthSchemeWiringTests.RealSchemeFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Traccar_typed_client_resolves_without_a_missing_handler_registration()
    {
        using var scope = _factory.Services.CreateScope();

        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<ITraccarClient>());
    }

    [Fact]
    public void Hologram_typed_client_resolves_without_a_missing_handler_registration()
    {
        using var scope = _factory.Services.CreateScope();

        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IHologramClient>());
    }
}
