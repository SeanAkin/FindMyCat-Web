using System.Globalization;
using System.Text.Json;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services;

public interface ITelemetryIngestService
{
    Task RecordStatusAsync(string uniqueId, JsonElement payload, CancellationToken cancellationToken = default);

    // A collar position published over LTE is always a GNSS fix (WiFi and cell go through
    // their own scan topics), so it is logged as the ping it answers.
    Task RecordGnssFixAsync(string uniqueId, string fixCsv, CancellationToken cancellationToken = default);
}

public sealed class TelemetryIngestService(IDeviceEventRepository deviceEventRepository, ILogger<TelemetryIngestService> logger) : ITelemetryIngestService
{
    private const string LegacyNetworkCommand = "NET";
    private const string DeviceNetworkKind = "network";
    private const string DeviceCommandKind = "command";

    public async Task RecordStatusAsync(string uniqueId, JsonElement payload, CancellationToken cancellationToken = default)
    {
        var report = StatusReport.FromPayload(payload);

        await deviceEventRepository.AddAsync(
            new DeviceEvent
            {
                UniqueId = uniqueId,
                Kind = report.Kind,
                Payload = report.Json,
                ReceivedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        logger.LogInformation("Stored {Kind} telemetry event for {UniqueId}", report.Kind, uniqueId);
    }

    public Task RecordGnssFixAsync(string uniqueId, string fixCsv, CancellationToken cancellationToken = default)
    {
        // Same layout the collar publishes for Traccar: lat,lon,satellites,accuracy_m,battery.
        var fields = fixCsv.Split(',');
        if (fields.Length < 4
            || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var satellites)
            || !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var accuracyMeters))
        {
            throw new TelemetryPayloadException($"GNSS fix was not lat,lon,satellites,accuracy: '{fixCsv}'");
        }

        var ping = new Dictionary<string, object>
        {
            ["kind"] = DeviceCommandKind,
            ["command"] = "ping",
            ["status"] = "ok",
            ["method"] = "gnss",
            ["satellites"] = satellites,
            ["accuracy_m"] = Math.Round(accuracyMeters)
        };
        return RecordStatusAsync(uniqueId, JsonSerializer.SerializeToElement(ping), cancellationToken);
    }

    private readonly record struct StatusReport(string Kind, string Json)
    {
        public static StatusReport FromPayload(JsonElement payload)
        {
            var body = payload.ValueKind == JsonValueKind.String ? Reparse(payload.GetString()) : payload;

            if (body.ValueKind != JsonValueKind.Object)
            {
                throw new TelemetryPayloadException($"payload must be a JSON object, was {body.ValueKind}");
            }

            return new StatusReport(ResolveKind(body), body.GetRawText());
        }

        private static JsonElement Reparse(string? json)
        {
            try
            {
                using var document = JsonDocument.Parse(json ?? string.Empty);
                return document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new TelemetryPayloadException("payload string was not valid JSON", exception);
            }
        }

        private static string ResolveKind(JsonElement body)
        {
            if (body.TryGetProperty("kind", out var kind)
                && kind.ValueKind == JsonValueKind.String
                && kind.GetString() is { Length: > 0 } explicitKind)
            {
                return explicitKind;
            }

            if (!body.TryGetProperty("command", out var command))
                throw new TelemetryPayloadException("payload had neither a 'kind' nor a 'command' field");
            
            var value = command.ValueKind == JsonValueKind.String ? command.GetString() : command.GetRawText();
            return value == LegacyNetworkCommand ? DeviceNetworkKind : DeviceCommandKind;

        }
    }
}
