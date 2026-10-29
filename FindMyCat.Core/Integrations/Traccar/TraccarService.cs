using FindMyCat.Core.Errors;
using FindMyCat.Core.Services;

namespace FindMyCat.Core.Integrations.Traccar;

public interface ITraccarService
{
    Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<TraccarPosition?> GetLatestPositionAsync(long deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraccarPosition>> GetHistoryAsync(long deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    Task<TraccarDevice> CreateDeviceAsync(string name, string uniqueId, string emoji, CancellationToken cancellationToken = default);

    Task<TraccarDevice> UpdateDeviceAsync(long deviceId, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default);

    Task DeleteDeviceAsync(long deviceId, CancellationToken cancellationToken = default);
}

public sealed class TraccarService(ICredentialService credentialService, ITraccarClient client) : ITraccarService
{
    public static readonly TimeSpan MaxHistoryRange = TimeSpan.FromDays(31);

    public async Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        return await client.GetDevicesWithPositionsAsync(token, cancellationToken);
    }

    public async Task<TraccarDevice> CreateDeviceAsync(
        string name, string uniqueId, string emoji, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        return await client.CreateDeviceAsync(token, name, uniqueId, emoji, cancellationToken);
    }

    public async Task<TraccarDevice> UpdateDeviceAsync(
        long deviceId, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        await RequireDeviceAsync(token, deviceId, cancellationToken);
        return await client.UpdateDeviceAsync(token, deviceId, name, uniqueId, emoji, cancellationToken);
    }

    public async Task DeleteDeviceAsync(long deviceId, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        await RequireDeviceAsync(token, deviceId, cancellationToken);
        await client.DeleteDeviceAsync(token, deviceId, cancellationToken);
    }

    // Traccar answers an unknown id with a generic error rather than 404, so check first.
    private async Task RequireDeviceAsync(string token, long deviceId, CancellationToken cancellationToken)
    {
        var devices = await client.GetDevicesWithPositionsAsync(token, cancellationToken);
        if (devices.All(d => d.Id != deviceId))
        {
            throw new DeviceNotFoundException(deviceId);
        }
    }

    public async Task<TraccarPosition?> GetLatestPositionAsync(long deviceId, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        var positions = await client.GetPositionsAsync(token, deviceId, from: null, to: null, cancellationToken);
        return positions.Count > 0 ? positions[0] : null;
    }

    public async Task<IReadOnlyList<TraccarPosition>> GetHistoryAsync(
        long deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (from >= to)
        {
            throw new InvalidHistoryRangeException();
        }

        if (to - from > MaxHistoryRange)
        {
            throw new HistoryRangeTooLargeException(MaxHistoryRange);
        }

        var token = await RequireTokenAsync(cancellationToken);
        return await client.GetPositionsAsync(token, deviceId, from, to, cancellationToken);
    }

    private async Task<string> RequireTokenAsync(CancellationToken cancellationToken)
    {
        var token = await credentialService.GetTraccarTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            throw new TraccarNotConfiguredException();
        }

        return token;
    }
}
