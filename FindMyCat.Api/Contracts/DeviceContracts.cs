using System.Text.Json;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.Core.Telemetry;

namespace FindMyCat.Api.Contracts;

public sealed record DeviceResponse(
    long Id,
    string Name,
    string UniqueId,
    string Status,
    DateTimeOffset? LastUpdate,
    bool Disabled,
    DeviceAttributes Attributes,
    PositionResponse? Position)
{
    public static DeviceResponse FromDomain(TraccarDevice device) => new(
        device.Id,
        device.Name,
        device.UniqueId,
        device.Status,
        device.LastUpdate,
        device.Disabled,
        new DeviceAttributes(device.Emoji),
        device.LatestPosition is null ? null : PositionResponse.FromDomain(device.LatestPosition));
}

// Same shape as Traccar's device attributes, so clients written against Traccar keep working.
public sealed record DeviceAttributes(string? Emoji);

public sealed record SaveDeviceRequest(string Name, string UniqueId, DeviceAttributes? Attributes);

public sealed record PositionResponse(
    long DeviceId,
    DateTimeOffset FixTime,
    DateTimeOffset DeviceTime,
    DateTimeOffset ServerTime,
    double Latitude,
    double Longitude,
    double Altitude,
    double SpeedKnots,
    double Course,
    double Accuracy,
    bool Valid,
    double? BatteryLevel,
    double? Satellites)
{
    public static PositionResponse FromDomain(TraccarPosition position) => new(
        position.DeviceId,
        position.FixTime,
        position.DeviceTime,
        position.ServerTime,
        position.Latitude,
        position.Longitude,
        position.Altitude,
        position.SpeedKnots,
        position.Course,
        position.Accuracy,
        position.Valid,
        position.BatteryLevel,
        position.Satellites);
}

public sealed record HistoryRangeRequest(DateTimeOffset? From, DateTimeOffset? To);

public sealed record DeviceEventsRequest(long? After, string? Kind, int? Limit);

public sealed record DeviceEventResponse(
    long Id,
    DateTimeOffset ReceivedAt,
    string Kind,
    object? Data,
    JsonElement Raw)
{
    public static DeviceEventResponse FromDomain(DeviceEvent deviceEvent) => new(
        deviceEvent.Id,
        deviceEvent.ReceivedAt,
        deviceEvent.Kind,
        DeviceTelemetry.Parse(deviceEvent.Kind, deviceEvent.Payload),
        RawPayload(deviceEvent.Payload));

    private static JsonElement RawPayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(payload);
        }
    }
}

public sealed record DeviceStatusResponse(
    long Id,
    string Name,
    string UniqueId,
    string Status,
    DateTimeOffset? LastUpdate,
    bool Disabled)
{
    public static DeviceStatusResponse FromDomain(TraccarDevice device) => new(
        device.Id,
        device.Name,
        device.UniqueId,
        device.Status,
        device.LastUpdate,
        device.Disabled);
}

public sealed record TraccarConnectionStatusResponse(bool Connected, string? Code)
{
    public static TraccarConnectionStatusResponse FromDomain(TraccarConnectionStatus status) => new(
        status.Connected,
        status.ErrorCode);
}
