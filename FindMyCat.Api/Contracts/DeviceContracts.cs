using FindMyCat.Core.Services.Traccar;

namespace FindMyCat.Api.Contracts;

public sealed record DeviceResponse(
    long Id,
    string Name,
    string UniqueId,
    string Status,
    DateTimeOffset? LastUpdate,
    bool Disabled,
    PositionResponse? Position)
{
    public static DeviceResponse FromDomain(TraccarDevice device) => new(
        device.Id,
        device.Name,
        device.UniqueId,
        device.Status,
        device.LastUpdate,
        device.Disabled,
        device.LatestPosition is null ? null : PositionResponse.FromDomain(device.LatestPosition));
}

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

public sealed record TraccarErrorResponse(string Code, string Message);
