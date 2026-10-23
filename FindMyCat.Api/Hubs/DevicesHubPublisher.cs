using FindMyCat.Api.Contracts;
using FindMyCat.Core.Integrations.Traccar.Socket;
using Microsoft.AspNetCore.SignalR;

namespace FindMyCat.Api.Hubs;

public sealed class DevicesHubPublisher(
    IHubContext<DevicesHub, IDevicesClient> hubContext,
    ILogger<DevicesHubPublisher> logger,
    TraccarConnectionStatus initialStatus) : ITraccarUpdatePublisher
{
    private readonly Lock _statusSendOrder = new();
    private TraccarConnectionStatus _status = initialStatus;

    public Task PublishAsync(TraccarSocketMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Devices.Count > 0)
        {
            QueueWithoutWaitingForSlowBrowsers(
                hubContext.Clients.All.DevicesUpdated(message.Devices.Select(DeviceStatusResponse.FromDomain).ToList()));
        }

        if (message.Positions.Count > 0)
        {
            QueueWithoutWaitingForSlowBrowsers(
                hubContext.Clients.All.PositionsUpdated(message.Positions.Select(PositionResponse.FromDomain).ToList()));
        }

        return Task.CompletedTask;
    }

    public Task PublishConnectionChangedAsync(TraccarConnectionStatus status, CancellationToken cancellationToken = default)
    {
        lock (_statusSendOrder)
        {
            if (status != _status)
            {
                _status = status;
                QueueWithoutWaitingForSlowBrowsers(
                    hubContext.Clients.All.TraccarConnectionChanged(TraccarConnectionStatusResponse.FromDomain(status)));
            }
        }

        return Task.CompletedTask;
    }

    public void SendCurrentStatusTo(string connectionId)
    {
        lock (_statusSendOrder)
        {
            QueueWithoutWaitingForSlowBrowsers(
                hubContext.Clients.Client(connectionId).TraccarConnectionChanged(TraccarConnectionStatusResponse.FromDomain(_status)));
        }
    }

    private void QueueWithoutWaitingForSlowBrowsers(Task delivery)
    {
        if (!delivery.IsCompletedSuccessfully)
        {
            _ = LogIfDeliveryFailsAsync(delivery);
        }
    }

    private async Task LogIfDeliveryFailsAsync(Task delivery)
    {
        try
        {
            await delivery;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deliver a live update to a browser.");
        }
    }
}
