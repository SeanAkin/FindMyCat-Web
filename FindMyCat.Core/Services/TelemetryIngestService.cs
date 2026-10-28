using System.Text.Json;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Telemetry;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services;

public interface ITelemetryIngestService
{
    Task RecordStatusAsync(string uniqueId, JsonElement payload, CancellationToken cancellationToken = default);
}

public sealed class TelemetryIngestService(IDeviceEventRepository deviceEventRepository, ILogger<TelemetryIngestService> logger) : ITelemetryIngestService
{
    private const string LegacyNetworkCommand = "NET";

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
            return value == LegacyNetworkCommand ? DeviceTelemetry.NetworkKind : DeviceTelemetry.CommandKind;

        }
    }
}
