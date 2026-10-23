namespace FindMyCat.Core.Services;

public interface IConnectedClients
{
    void DisconnectUser(Guid userId);
}
