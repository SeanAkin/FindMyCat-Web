namespace FindMyCat.Core.Services.Traccar;

public interface ITraccarService
{
    Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<TraccarPosition?> GetLatestPositionAsync(long deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraccarPosition>> GetHistoryAsync(long deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class TraccarService(ICredentialService credentialService, ITraccarClient client) : ITraccarService
{
    public async Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        return await client.GetDevicesWithPositionsAsync(token, cancellationToken);
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
