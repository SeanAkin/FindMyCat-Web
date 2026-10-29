using FindMyCat.Core.Integrations.Traccar;

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

    public Task<TraccarDevice> CreateDeviceAsync(
        string token, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default)
    {
        if (ThrowInstead is not null)
        {
            return Task.FromException<TraccarDevice>(ThrowInstead);
        }

        var device = new TraccarDevice(
            Devices.Select(d => d.Id).DefaultIfEmpty().Max() + 1, name, uniqueId, "unknown", null, false, null, emoji);
        Devices = [.. Devices, device];
        return Task.FromResult(device);
    }

    public Task<TraccarDevice> UpdateDeviceAsync(
        string token, long deviceId, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default)
    {
        if (ThrowInstead is not null)
        {
            return Task.FromException<TraccarDevice>(ThrowInstead);
        }

        var updated = Devices.Single(d => d.Id == deviceId) with { Name = name, UniqueId = uniqueId, Emoji = emoji };
        Devices = [.. Devices.Select(d => d.Id == deviceId ? updated : d)];
        return Task.FromResult(updated);
    }

    public Task DeleteDeviceAsync(string token, long deviceId, CancellationToken cancellationToken = default)
    {
        if (ThrowInstead is not null)
        {
            return Task.FromException(ThrowInstead);
        }

        Devices = [.. Devices.Where(d => d.Id != deviceId)];
        return Task.CompletedTask;
    }
}