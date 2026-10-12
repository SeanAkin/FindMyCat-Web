using System.Text.Json;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public sealed record TraccarSocketMessage(IReadOnlyList<TraccarDevice> Devices, IReadOnlyList<TraccarPosition> Positions)
{
    public bool IsEmpty => Devices.Count == 0 && Positions.Count == 0;

    internal static TraccarSocketMessage Parse(ReadOnlySpan<byte> utf8Json)
    {
        var message = JsonSerializer.Deserialize<MessageDto>(utf8Json, TraccarWireFormat.JsonOptions);
        return new TraccarSocketMessage(
            message?.Devices?.Select(d => d.ToDomain(latestPosition: null)).ToList() ?? [],
            message?.Positions?.Select(p => p.ToDomain()).ToList() ?? []);
    }

    private sealed record MessageDto(List<TraccarDeviceDto>? Devices, List<TraccarPositionDto>? Positions);
}
