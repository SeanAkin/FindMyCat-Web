using FindMyCat.Api.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace FindMyCat.Api.Hubs;

public interface IDevicesClient
{
    Task DevicesUpdated(IReadOnlyList<DeviceStatusResponse> devices);

    Task PositionsUpdated(IReadOnlyList<PositionResponse> positions);

    Task TraccarConnectionChanged(TraccarConnectionStatusResponse status);
}

public sealed class DevicesHub(DevicesHubPublisher publisher, DevicesHubConnections connections) : Hub<IDevicesClient>
{
    public const string Route = "/hubs/devices";

    public override Task OnConnectedAsync()
    {
        connections.Add(Context);
        publisher.SendCurrentStatusTo(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context);
        return base.OnDisconnectedAsync(exception);
    }
}
