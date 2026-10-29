using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.Core.Services;

namespace FindMyCat.Api.Hubs;

public sealed class LiveClients(
    DevicesHubPublisher hub,
    DevicesHubConnections hubConnections,
    TraccarSocketPassthrough socket) : ITraccarUpdatePublisher, IConnectedClients
{
    public async Task PublishAsync(TraccarSocketMessage message, CancellationToken cancellationToken = default)
    {
        await hub.PublishAsync(message, cancellationToken);
        await socket.PublishAsync(message, cancellationToken);
    }

    public async Task PublishConnectionChangedAsync(TraccarConnectionStatus status, CancellationToken cancellationToken = default)
    {
        await hub.PublishConnectionChangedAsync(status, cancellationToken);
        await socket.PublishConnectionChangedAsync(status, cancellationToken);
    }

    public void DisconnectUser(Guid userId)
    {
        hubConnections.DisconnectUser(userId);
        socket.DisconnectUser(userId);
    }
}