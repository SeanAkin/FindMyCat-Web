using System.Globalization;
using System.Text.Json.Nodes;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.Traccar.Socket;

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
    long Id,
    long DeviceId,
    DateTimeOffset FixTime,
    DateTimeOffset DeviceTime,
    DateTimeOffset ServerTime,
    double Latitude,
    double Longitude,
    double Altitude,
    double Speed,
    double Course,
    double Accuracy,
    bool Valid,
    PositionAttributes Attributes)
{
    public static PositionResponse FromDomain(TraccarPosition position) => new(
        position.Id,
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
        new PositionAttributes(position.BatteryLevel, position.Satellites));
}

public sealed record PositionAttributes(double? BatteryLevel, double? Sat);

public sealed record HistoryRangeRequest(DateTimeOffset? From, DateTimeOffset? To);

public sealed record DeviceEventsRequest(long? After, string? Kind, int? Limit);

public static class DeviceEventResponse
{
    public static JsonObject State(IEnumerable<DeviceEvent> latestPerKind)
    {
        var state = new JsonObject();
        foreach (var deviceEvent in latestPerKind)
        {
            var fields = Payload(deviceEvent) as JsonObject ?? new JsonObject();
            fields["id"] = deviceEvent.Id;
            fields["receivedAt"] = Timestamp(deviceEvent.ReceivedAt);
            state[deviceEvent.Kind] = fields;
        }

        return state;
    }

    public static JsonObject Event(DeviceEvent deviceEvent) => new()
    {
        ["id"] = deviceEvent.Id,
        ["receivedAt"] = Timestamp(deviceEvent.ReceivedAt),
        ["kind"] = deviceEvent.Kind,
        ["payload"] = Payload(deviceEvent)
    };

    private static JsonNode? Payload(DeviceEvent deviceEvent) => JsonNode.Parse(deviceEvent.Payload);

    // Whole seconds: Swift's ISO 8601 decoding rejects fractional ones.
    private static string Timestamp(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
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
