using System.Text.Json;
using System.Text.Json.Serialization;

namespace FindMyCat.Core.Integrations.Traccar;

internal static class TraccarWireFormat
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

internal sealed record TraccarDeviceDto(
    long Id,
    string? Name,
    string? UniqueId,
    string? Status,
    DateTimeOffset? LastUpdate,
    bool Disabled)
{
    public TraccarDevice ToDomain(TraccarPosition? latestPosition) => new(
        Id,
        Name ?? string.Empty,
        UniqueId ?? string.Empty,
        Status ?? "unknown",
        LastUpdate,
        Disabled,
        latestPosition);
}

internal sealed record TraccarPositionDto(
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
    TraccarPositionAttributesDto? Attributes)
{
    public TraccarPosition ToDomain() => new(
        DeviceId,
        FixTime,
        DeviceTime,
        ServerTime,
        Latitude,
        Longitude,
        Altitude,
        Speed,
        Course,
        Accuracy,
        Valid,
        Attributes?.BatteryLevel,
        Attributes?.Sat);
}

internal sealed record TraccarPositionAttributesDto(
    [property: JsonPropertyName("batteryLevel")] double? BatteryLevel,
    [property: JsonPropertyName("sat")] double? Sat);
