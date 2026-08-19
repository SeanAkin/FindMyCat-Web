using FindMyCat.Core.Services.Hologram;

namespace FindMyCat.IntegrationTests.Stubs;

public sealed class StubHologramClient : IHologramClient
{
    public Dictionary<string, int> DeviceIdsByImei { get; set; } = new();
    public List<(int HologramDeviceId, string Command)> SentMessages { get; } = [];
    public Exception? ThrowInstead { get; set; }

    public void Reset()
    {
        DeviceIdsByImei = new Dictionary<string, int>();
        SentMessages.Clear();
        ThrowInstead = null;
    }

    public Task<int?> FindDeviceIdByImeiAsync(string apiKey, string imei, CancellationToken cancellationToken = default) =>
        ThrowInstead is not null
            ? Task.FromException<int?>(ThrowInstead)
            : Task.FromResult(DeviceIdsByImei.TryGetValue(imei, out var id) ? id : (int?)null);

    public Task SendMessageAsync(string apiKey, int hologramDeviceId, string command, CancellationToken cancellationToken = default)
    {
        if (ThrowInstead is not null)
        {
            return Task.FromException(ThrowInstead);
        }

        SentMessages.Add((hologramDeviceId, command));
        return Task.CompletedTask;
    }
}
