using FindMyCat.Api.Contracts;
using FindMyCat.Api.Hubs;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.Traccar.Socket;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class DevicesHubPublisherTests
{
    private static readonly DateTimeOffset FixTime = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IDevicesClient> _everyone = new();
    private readonly Mock<IDevicesClient> _newBrowser = new();
    private readonly Mock<IHubContext<DevicesHub, IDevicesClient>> _hubContext = new();
    private readonly DevicesHubPublisher _sut;

    public DevicesHubPublisherTests()
    {
        var clients = new Mock<IHubClients<IDevicesClient>>();
        clients.Setup(c => c.All).Returns(_everyone.Object);
        clients.Setup(c => c.Client("new-browser")).Returns(_newBrowser.Object);
        _hubContext.Setup(h => h.Clients).Returns(clients.Object);

        _sut = CreatePublisher(TraccarConnectionStatus.Unavailable);
    }

    private DevicesHubPublisher CreatePublisher(TraccarConnectionStatus initialStatus) =>
        new(_hubContext.Object, NullLogger<DevicesHubPublisher>.Instance, initialStatus);

    [Fact]
    public void Publishing_does_not_wait_for_a_browser_that_is_slow_to_receive()
    {
        var neverDelivered = new TaskCompletionSource();
        _everyone.Setup(c => c.PositionsUpdated(It.IsAny<IReadOnlyList<PositionResponse>>())).Returns(neverDelivered.Task);

        var publish = _sut.PublishAsync(new TraccarSocketMessage(
            [],
            [new TraccarPosition(7, FixTime, FixTime, FixTime, 1.5, 2.5, 0, 0, 0, 5, true, null, null)]),
            TestContext.Current.CancellationToken);

        publish.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void A_browser_that_connects_before_traccar_reports_gets_the_initial_status()
    {
        _newBrowser.Setup(c => c.TraccarConnectionChanged(It.IsAny<TraccarConnectionStatusResponse>())).Returns(Task.CompletedTask);

        CreatePublisher(TraccarConnectionStatus.LiveUpdatesDisabled).SendCurrentStatusTo("new-browser");

        _newBrowser.Verify(c => c.TraccarConnectionChanged(
            TraccarConnectionStatusResponse.FromDomain(TraccarConnectionStatus.LiveUpdatesDisabled)));
    }

    [Fact]
    public async Task An_unchanged_status_is_not_broadcast_again()
    {
        _everyone.Setup(c => c.TraccarConnectionChanged(It.IsAny<TraccarConnectionStatusResponse>())).Returns(Task.CompletedTask);

        await _sut.PublishConnectionChangedAsync(TraccarConnectionStatus.Up, TestContext.Current.CancellationToken);
        await _sut.PublishConnectionChangedAsync(TraccarConnectionStatus.Up, TestContext.Current.CancellationToken);

        _everyone.Verify(c => c.TraccarConnectionChanged(It.IsAny<TraccarConnectionStatusResponse>()), Times.Once);
    }
}
