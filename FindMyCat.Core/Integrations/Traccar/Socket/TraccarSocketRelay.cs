using System.Diagnostics;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public sealed class TraccarSocketRelay(
    IServiceScopeFactory scopeFactory,
    ITraccarUpdatePublisher publisher,
    TraccarCredentialChangeSignal credentialChangeSignal,
    ILogger<TraccarSocketRelay> logger) : BackgroundService
{
    private static readonly TimeSpan CredentialRejectedRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StableConnectionDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumReconnectDelay = TimeSpan.FromSeconds(30);

    private enum ConnectionOutcome
    {
        NotConfigured,
        CredentialRejected,
        Interrupted,
        Failed,
        DroppedAfterStableConnection
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, credentialChangeSignal.ChangedToken);

            var outcome = await ConnectAndRelayAsync(attempt.Token);

            consecutiveFailures = outcome switch
            {
                ConnectionOutcome.Failed => consecutiveFailures + 1,
                ConnectionOutcome.DroppedAfterStableConnection => 1,
                _ => 0
            };

            var retryDelay = outcome switch
            {
                ConnectionOutcome.NotConfigured => Timeout.InfiniteTimeSpan,
                ConnectionOutcome.CredentialRejected => CredentialRejectedRetryDelay,
                ConnectionOutcome.Interrupted => TimeSpan.Zero,
                _ => ReconnectDelayAfter(consecutiveFailures)
            };

            await WaitAsync(retryDelay, attempt.Token);
        }
    }

    private async Task<ConnectionOutcome> ConnectAndRelayAsync(CancellationToken cancellationToken)
    {
        long? connectedAt = null;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var token = await scope.ServiceProvider.GetRequiredService<ICredentialService>().GetTraccarTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                logger.LogInformation("Traccar is not configured; live updates will start once a token is added.");
                return ConnectionOutcome.NotConfigured;
            }

            var socketClient = scope.ServiceProvider.GetRequiredService<ITraccarSocketClient>();
            await using var connection = await socketClient.ConnectAsync(token, cancellationToken);

            connectedAt = Stopwatch.GetTimestamp();
            logger.LogInformation("Connected to the Traccar socket.");
            await BroadcastAsync(() => publisher.PublishConnectionChangedAsync(connected: true, cancellationToken), cancellationToken);

            await foreach (var message in connection.ReadMessagesAsync(cancellationToken))
            {
                await BroadcastAsync(() => publisher.PublishAsync(message, cancellationToken), cancellationToken);
            }

            logger.LogWarning("Traccar closed the socket connection.");
            return OutcomeOfDroppedConnection(connectedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ConnectionOutcome.Interrupted;
        }
        catch (TraccarCredentialRejectedException ex)
        {
            logger.LogWarning(
                "Traccar rejected the stored token ({Detail}); retrying in {RetryDelay} or when the token changes.",
                ex.LogDetail, CredentialRejectedRetryDelay);
            return ConnectionOutcome.CredentialRejected;
        }
        catch (TraccarUnavailableException ex)
        {
            logger.LogWarning("Traccar socket is unavailable: {Detail}", ex.LogDetail);
            return OutcomeOfDroppedConnection(connectedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure in the Traccar socket connection.");
            return OutcomeOfDroppedConnection(connectedAt);
        }
        finally
        {
            if (connectedAt is not null)
            {
                await BroadcastAsync(() => publisher.PublishConnectionChangedAsync(connected: false, CancellationToken.None), CancellationToken.None);
            }
        }
    }

    // A failed broadcast to browsers must not tear down the Traccar connection, or escape the
    // finally block and stop the host.
    private async Task BroadcastAsync(Func<Task> broadcast, CancellationToken cancellationToken)
    {
        try
        {
            await broadcast();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to broadcast a Traccar update to connected browsers.");
        }
    }

    internal static TimeSpan ReconnectDelayAfter(int consecutiveFailures) =>
        TimeSpan.FromSeconds(Math.Min(MaximumReconnectDelay.TotalSeconds, Math.Pow(2, consecutiveFailures - 1)));

    private static ConnectionOutcome OutcomeOfDroppedConnection(long? connectedAt) =>
        connectedAt is not null && Stopwatch.GetElapsedTime(connectedAt.Value) >= StableConnectionDuration
            ? ConnectionOutcome.DroppedAfterStableConnection
            : ConnectionOutcome.Failed;

    private static async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
