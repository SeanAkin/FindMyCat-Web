namespace FindMyCat.Core.Integrations.Traccar;

public sealed record TraccarDevice(
    long Id,
    string Name,
    string UniqueId,
    string Status,
    DateTimeOffset? LastUpdate,
    bool Disabled,
    TraccarPosition? LatestPosition);
