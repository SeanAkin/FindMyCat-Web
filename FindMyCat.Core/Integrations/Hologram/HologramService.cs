using System.Text.Json;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Services;

namespace FindMyCat.Core.Integrations.Hologram;

public interface IHologramService
{
    Task SendCommandAsync(long traccarDeviceId, HologramCommand command, CancellationToken cancellationToken = default);
}


public sealed class HologramService(
    ICredentialService credentialService,
    ITraccarService traccarService,
    IHologramClient client,
    ITelemetryIngestService telemetry) : IHologramService
{
    public async Task SendCommandAsync(long traccarDeviceId, HologramCommand command, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var imei = await GetDeviceImeiAsync(traccarDeviceId, cancellationToken);
        var commandValue = command.ToStringValue();

        string? messageId;
        try
        {
            var hologramDeviceId = await client.FindDeviceIdByImeiAsync(apiKey, imei, cancellationToken)
                                   ?? throw new HologramDeviceNotFoundException($"no Hologram device registered with IMEI '{imei}'");
            messageId = await client.SendMessageAsync(apiKey, hologramDeviceId, commandValue, cancellationToken);
        }
        catch (FindMyCatException)
        {
            await RecordAsync(imei, new() { ["command"] = commandValue, ["status"] = "fail", ["reason"] = "could not reach hologram" }, cancellationToken);
            throw;
        }

        await RecordAsync(imei, new() { ["command"] = commandValue, ["status"] = "sent", ["messageId"] = messageId }, cancellationToken);
    }

    // Logged beside the collar's own reports, so the app sees a command was sent before the collar answers.
    private Task RecordAsync(string imei, Dictionary<string, string?> fields, CancellationToken cancellationToken)
    {
        fields["kind"] = "command";
        return telemetry.RecordStatusAsync(imei, JsonSerializer.SerializeToElement(fields), cancellationToken);
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
