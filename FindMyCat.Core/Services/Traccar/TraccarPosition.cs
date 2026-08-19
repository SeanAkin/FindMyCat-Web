namespace FindMyCat.Core.Services.Traccar;

public sealed record TraccarPosition(
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
    double? Satellites);
