using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.WifiLocation;

namespace FindMyCat.IntegrationTests.Stubs;

public sealed class StubGoogleGeolocationClient : IGoogleGeolocationClient
{
    public GeoFix Fix { get; set; } = new(47.61, -122.33, 24.5);
    public string? FailWith { get; set; }

    public void Reset()
    {
        Fix = new GeoFix(47.61, -122.33, 24.5);
        FailWith = null;
    }

    public Task<GeoFix> LocateAsync(IReadOnlyList<WifiAccessPoint> accessPoints, CancellationToken cancellationToken = default) =>
        FailWith is not null
            ? Task.FromException<GeoFix>(new WifiLocateException(FailWith))
            : Task.FromResult(Fix);
}

public sealed class StubTraccarPositionClient : ITraccarPositionClient
{
    public List<(string UniqueId, GeoFix Fix, int? BatteryPercent)> Positions { get; } = [];

    public void Reset() => Positions.Clear();

    public Task AddPositionAsync(string uniqueId, GeoFix fix, int? batteryPercent, CancellationToken cancellationToken = default)
    {
        Positions.Add((uniqueId, fix, batteryPercent));
        return Task.CompletedTask;
    }
}