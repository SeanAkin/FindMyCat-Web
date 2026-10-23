using System.Collections.Concurrent;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace FindMyCat.Api.Hubs;

public sealed class DevicesHubConnections : IConnectedClients
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _connections = new();

    public void Add(HubCallerContext connection) => _connections[connection.ConnectionId] = connection;

    public void Remove(HubCallerContext connection) => _connections.TryRemove(connection.ConnectionId, out _);

    public void DisconnectUser(Guid userId)
    {
        foreach (var connection in _connections.Values.Where(c => BelongsTo(c, userId)))
        {
            connection.Abort();
        }
    }

    private static bool BelongsTo(HubCallerContext connection, Guid userId) =>
        Guid.TryParse(connection.UserIdentifier, out var connectionUserId) && connectionUserId == userId;
}
