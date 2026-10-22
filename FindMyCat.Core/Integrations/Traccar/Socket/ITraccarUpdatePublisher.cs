namespace FindMyCat.Core.Integrations.Traccar.Socket;

public interface ITraccarUpdatePublisher
{
    Task PublishAsync(TraccarSocketMessage message, CancellationToken cancellationToken = default);

    Task PublishConnectionChangedAsync(TraccarConnectionStatus status, CancellationToken cancellationToken = default);
}
