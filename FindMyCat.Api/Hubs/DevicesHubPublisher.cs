using FindMyCat.Api.Contracts;
using FindMyCat.Core.Integrations.Traccar.Socket;
using Microsoft.AspNetCore.SignalR;

namespace FindMyCat.Api.Hubs;

public sealed class DevicesHubPublisher(IHubContext<DevicesHub, IDevicesClient> hubContext) : ITraccarUpdatePublisher
{
    private volatile bool _isConnected;

    public bool IsConnected => _isConnected;

    public async Task PublishAsync(TraccarSocketMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Devices.Count > 0)
        {
            await hubContext.Clients.All.DevicesUpdated(message.Devices.Select(DeviceStatusResponse.FromDomain).ToList());
        }

        if (message.Positions.Count > 0)
        {
            await hubContext.Clients.All.PositionsUpdated(message.Positions.Select(PositionResponse.FromDomain).ToList());
        }
    }

    public Task PublishConnectionChangedAsync(bool connected, CancellationToken cancellationToken = default)
    {
        _isConnected = connected;
        return hubContext.Clients.All.TraccarConnectionChanged(new TraccarConnectionStatusResponse(connected));
    }
}
