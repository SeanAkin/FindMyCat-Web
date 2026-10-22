using System.Threading.Channels;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarSocketRelayTests
{
    private static readonly DateTimeOffset FixTime = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<ICredentialService> _credentials = new();
    private readonly Mock<ITraccarSocketClient> _socketClient = new();
    private readonly Mock<ITraccarUpdatePublisher> _publisher = new();
    private readonly TraccarCredentialChangeSignal _credentialChangeSignal = new();
    private readonly Channel<StubConnection> _connections = Channel.CreateUnbounded<StubConnection>();
    private readonly TraccarSocketRelay _sut;

    public TraccarSocketRelayTests()
    {
        _credentials.Setup(c => c.GetTraccarTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("stored-token");
        _socketClient.Setup(c => c.ConnectAsync("stored-token", It.IsAny<CancellationToken>())).ReturnsAsync(OpenConnection);

        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(ICredentialService))).Returns(_credentials.Object);
        services.Setup(s => s.GetService(typeof(ITraccarSocketClient))).Returns(_socketClient.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(services.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _sut = new TraccarSocketRelay(
            scopeFactory.Object,
            _publisher.Object,
            _credentialChangeSignal,
            NullLogger<TraccarSocketRelay>.Instance);
    }

    private ITraccarSocketConnection OpenConnection()
    {
        var connection = new StubConnection();
        _connections.Writer.TryWrite(connection);
        return connection;
    }

    private static TraccarSocketMessage PositionMessage(long deviceId) =>
        new([], [new TraccarPosition(deviceId, FixTime, FixTime, FixTime, 1.5, 2.5, 0, 0, 0, 5, true, null, null)]);

    private static CancellationToken Timeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout.Token;
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 30)]
    [InlineData(50, 30)]
    public void ReconnectDelayAfter_doubles_up_to_the_cap(int consecutiveFailures, double expectedSeconds)
    {
        TraccarSocketRelay.ReconnectDelayAfter(consecutiveFailures).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public async Task A_failed_broadcast_does_not_drop_the_traccar_connection()
    {
        var failing = PositionMessage(deviceId: 1);
        var next = PositionMessage(deviceId: 2);
        var relayed = Channel.CreateUnbounded<TraccarSocketMessage>();
        _publisher.Setup(p => p.PublishAsync(failing, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub send failed"));
        _publisher.Setup(p => p.PublishAsync(next, It.IsAny<CancellationToken>()))
            .Callback(() => relayed.Writer.TryWrite(next))
            .Returns(Task.CompletedTask);

        await _sut.StartAsync(TestContext.Current.CancellationToken);
        var connection = await _connections.Reader.ReadAsync(Timeout());
        connection.Send(failing);
        connection.Send(next);

        (await relayed.Reader.ReadAsync(Timeout())).ShouldBeSameAs(next);
        _socketClient.Verify(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        await _sut.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_failed_status_broadcast_does_not_stop_the_service()
    {
        _publisher.Setup(p => p.PublishConnectionChangedAsync(It.IsAny<TraccarConnectionStatus>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub send failed"));

        await _sut.StartAsync(TestContext.Current.CancellationToken);
        var first = await _connections.Reader.ReadAsync(Timeout());
        first.Drop();

        await _connections.Reader.ReadAsync(Timeout());
        _sut.ExecuteTask!.IsFaulted.ShouldBeFalse();
        await _sut.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_rejected_token_is_reported_so_an_admin_knows_to_re_enter_it()
    {
        var published = Channel.CreateUnbounded<TraccarConnectionStatus>();
        _publisher.Setup(p => p.PublishConnectionChangedAsync(It.IsAny<TraccarConnectionStatus>(), It.IsAny<CancellationToken>()))
            .Callback<TraccarConnectionStatus, CancellationToken>((status, _) => published.Writer.TryWrite(status))
            .Returns(Task.CompletedTask);
        _socketClient.Setup(c => c.ConnectAsync("stored-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TraccarCredentialRejectedException());

        await _sut.StartAsync(TestContext.Current.CancellationToken);

        (await published.Reader.ReadAsync(Timeout())).ShouldBe(TraccarConnectionStatus.CredentialRejected);
        await _sut.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Saving_a_new_token_does_not_report_traccar_as_unavailable_while_reconnecting()
    {
        var published = Channel.CreateUnbounded<TraccarConnectionStatus>();
        _publisher.Setup(p => p.PublishConnectionChangedAsync(It.IsAny<TraccarConnectionStatus>(), It.IsAny<CancellationToken>()))
            .Callback<TraccarConnectionStatus, CancellationToken>((status, _) => published.Writer.TryWrite(status))
            .Returns(Task.CompletedTask);

        await _sut.StartAsync(TestContext.Current.CancellationToken);
        (await published.Reader.ReadAsync(Timeout())).ShouldBe(TraccarConnectionStatus.Up);

        _credentialChangeSignal.NotifyChanged();

        (await published.Reader.ReadAsync(Timeout())).ShouldBe(TraccarConnectionStatus.Up);
        _socketClient.Verify(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        await _sut.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class StubConnection : ITraccarSocketConnection
    {
        private readonly Channel<TraccarSocketMessage> _messages = Channel.CreateUnbounded<TraccarSocketMessage>();

        public void Send(TraccarSocketMessage message) => _messages.Writer.TryWrite(message);

        public void Drop() => _messages.Writer.TryComplete();

        public IAsyncEnumerable<TraccarSocketMessage> ReadMessagesAsync(CancellationToken cancellationToken = default) =>
            _messages.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
