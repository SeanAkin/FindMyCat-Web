using FindMyCat.Core.Services.Traccar;

namespace FindMyCat.Core.Services.Hologram;

public interface IHologramService
{
    Task SendCommandAsync(long traccarDeviceId, HologramCommand command, CancellationToken cancellationToken = default);
}


public sealed class HologramService(ICredentialService credentialService, ITraccarService traccarService, IHologramClient client) : IHologramService
{
    public async Task SendCommandAsync(long traccarDeviceId, HologramCommand command, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var imei = await GetDeviceImeiAsync(traccarDeviceId, cancellationToken);

        var hologramDeviceId = await client.FindDeviceIdByImeiAsync(apiKey, imei, cancellationToken);
        if (hologramDeviceId is null)
        {
            throw new HologramDeviceNotFoundException($"no Hologram device registered with IMEI '{imei}'");
        }

        await client.SendMessageAsync(apiKey, hologramDeviceId.Value, command.ToStringValue(), cancellationToken);
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        var apiKey = await credentialService.GetHologramKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new HologramNotConfiguredException();
        }

        return apiKey;
    }

    private async Task<string> GetDeviceImeiAsync(long traccarDeviceId, CancellationToken cancellationToken)
    {
        var devices = await traccarService.GetDevicesAsync(cancellationToken);
        var device = devices.FirstOrDefault(d => d.Id == traccarDeviceId);
        if (device is null || string.IsNullOrEmpty(device.UniqueId))
        {
            throw new HologramDeviceNotFoundException($"no Traccar device with id {traccarDeviceId}");
        }

        return device.UniqueId;
    }
}
