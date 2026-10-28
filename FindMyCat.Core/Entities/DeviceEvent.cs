namespace FindMyCat.Core.Entities;

public class DeviceEvent
{
    public long Id { get; set; }

    public required string UniqueId { get; set; }

    public required string Kind { get; set; }

    public required string Payload { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
