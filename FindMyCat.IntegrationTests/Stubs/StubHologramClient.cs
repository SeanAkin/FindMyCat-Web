using FindMyCat.Core.Integrations.Hologram;

namespace FindMyCat.IntegrationTests.Stubs;

public sealed class StubHologramClient : IHologramClient
{
    public Dictionary<string, int> DeviceIdsByImei { get; set; } = new();
    public List<(int HologramDeviceId, string Command)> SentMessages { get; } = [];
    public Dictionary<int, HologramSimStatus> SimStatuses { get; } = new();
    public Exception? ThrowInstead { get; set; }

    public void Reset()
    {
        DeviceIdsByImei = new Dictionary<string, int>();
        SentMessages.Clear();
        SimStatuses.Clear();
        ThrowInstead = null;
    }

    public Task<int?> FindDeviceIdByImeiAsync(string apiKey, string imei, CancellationToken cancellationToken = default) =>
        ThrowInstead is not null
            ? Task.FromException<int?>(ThrowInstead)
            : Task.FromResult(DeviceIdsByImei.TryGetValue(imei, out var id) ? id : (int?)null);

    public Task<string?> SendMessageAsync(string apiKey, int hologramDeviceId, string command, CancellationToken cancellationToken = default)
    {
        if (ThrowInstead is not null)
        {
            return Task.FromException<string?>(ThrowInstead);
        }

        SentMessages.Add((hologramDeviceId, command));
        return Task.FromResult<string?>($"message-{SentMessages.Count}");
    }

    public Task<HologramSimStatus> GetSimStatusAsync(string apiKey, int hologramDeviceId, CancellationToken cancellationToken = default) =>
        ThrowInstead is not null
            ? Task.FromException<HologramSimStatus>(ThrowInstead)
            : Task.FromResult(SimStatuses.GetValueOrDefault(hologramDeviceId, new HologramSimStatus(null, null, null)));
}
