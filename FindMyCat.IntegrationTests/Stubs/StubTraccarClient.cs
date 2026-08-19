using FindMyCat.Core.Services.Traccar;

namespace FindMyCat.IntegrationTests.Stubs;

public sealed class StubTraccarClient : ITraccarClient
{
    public IReadOnlyList<TraccarDevice> Devices { get; set; } = [];
    public IReadOnlyList<TraccarPosition> Positions { get; set; } = [];
    public Exception? ThrowInstead { get; set; }

    public void Reset()
    {
        Devices = [];
        Positions = [];
        ThrowInstead = null;
    }

    public Task<IReadOnlyList<TraccarDevice>> GetDevicesWithPositionsAsync(string token, CancellationToken cancellationToken = default) =>
        ThrowInstead is not null
            ? Task.FromException<IReadOnlyList<TraccarDevice>>(ThrowInstead)
            : Task.FromResult(Devices);

    public Task<IReadOnlyList<TraccarPosition>> GetPositionsAsync(
        string token, long deviceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default) =>
        ThrowInstead is not null
            ? Task.FromException<IReadOnlyList<TraccarPosition>>(ThrowInstead)
            : Task.FromResult(Positions);
}
