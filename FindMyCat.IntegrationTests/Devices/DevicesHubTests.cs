using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using FindMyCat.Api.Contracts;
using FindMyCat.Api.Hubs;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class DevicesHubTests : IntegrationTestBase
{
    private static readonly DateTimeOffset FixTime = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly TraccarConnectionStatusResponse Connected = TraccarConnectionStatusResponse.FromDomain(TraccarConnectionStatus.Up);
    private static readonly TraccarConnectionStatusResponse NotConfigured = TraccarConnectionStatusResponse.FromDomain(TraccarConnectionStatus.NotConfigured);
    private static readonly TraccarConnectionStatusResponse CredentialRejected = TraccarConnectionStatusResponse.FromDomain(TraccarConnectionStatus.CredentialRejected);
    private static readonly TraccarConnectionStatusResponse Unavailable = TraccarConnectionStatusResponse.FromDomain(TraccarConnectionStatus.Unavailable);

    public DevicesHubTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Unauthenticated_negotiate_is_rejected()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(
            $"{DevicesHub.Route}/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unauthenticated_websocket_connection_is_rejected()
    {
        await using var hub = CreateHubConnection(user: null);

        await Should.ThrowAsync<Exception>(() => hub.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Websocket_connection_from_another_origin_is_rejected()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        await using var hub = CreateHubConnection(user, origin: "https://other.example.com");

        await Should.ThrowAsync<Exception>(() => hub.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Websocket_connection_from_the_app_origin_is_accepted()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        await using var hub = CreateHubConnection(user, origin: Factory.Server.BaseAddress.GetLeftPart(UriPartial.Authority));

        await hub.StartAsync(TestContext.Current.CancellationToken);

        hub.State.ShouldBe(HubConnectionState.Connected);
    }

    [Fact]
    public async Task Hub_closes_the_connection_when_the_sign_in_expires()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        await using var hub = CreateHubConnection(user, signInExpiresAt: DateTimeOffset.UtcNow.AddSeconds(1));
        var closed = ClosedSignal(hub);

        await hub.StartAsync(TestContext.Current.CancellationToken);

        await closed.WaitAsync(Timeout());
    }

    [Fact]
    public async Task Reports_not_configured_when_traccar_has_no_token()
    {
        await using var listener = await ListenAsync();

        await listener.WaitForStatusAsync(NotConfigured);
        TraccarSocket.ConnectAttempts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Configuring_a_token_connects_to_traccar_and_reports_connected()
    {
        await using var listener = await ListenAsync();

        await ConfigureTraccarTokenAsync();

        var connection = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        connection.Token.ShouldBe("stored-token");
        await listener.WaitForStatusAsync(Connected);
    }

    [Fact]
    public async Task Positions_from_traccar_are_relayed_to_listeners()
    {
        await using var listener = await ListenAsync();
        await ConfigureTraccarTokenAsync();
        var connection = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        connection.Send(new TraccarSocketMessage(
            [],
            [new TraccarPosition(7, FixTime, FixTime, FixTime, 12.34, 56.78, 0, 0, 0, 8, true, 55, 7)]));

        var position = (await listener.Positions.Reader.ReadAsync(Timeout())).ShouldHaveSingleItem();
        position.DeviceId.ShouldBe(7);
        position.Latitude.ShouldBe(12.34);
        position.BatteryLevel.ShouldBe(55);
    }

    [Fact]
    public async Task Device_changes_from_traccar_are_relayed_to_listeners()
    {
        await using var listener = await ListenAsync();
        await ConfigureTraccarTokenAsync();
        var connection = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        connection.Send(new TraccarSocketMessage(
            [new TraccarDevice(7, "Test Collar", "unique-7", "offline", FixTime, false, LatestPosition: null)],
            []));

        var device = (await listener.Devices.Reader.ReadAsync(Timeout())).ShouldHaveSingleItem();
        device.Id.ShouldBe(7);
        device.Name.ShouldBe("Test Collar");
        device.Status.ShouldBe("offline");
        device.LastUpdate.ShouldBe(FixTime);
    }

    [Fact]
    public async Task Changing_the_token_reconnects_with_the_new_token()
    {
        await ConfigureTraccarTokenAsync();
        var first = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        await SetTraccarTokenAsync("rotated-token");

        var second = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        second.Token.ShouldBe("rotated-token");
        first.Disposed.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Deleting_the_token_disconnects_and_reports_disconnected()
    {
        await using var listener = await ListenAsync();
        await ConfigureTraccarTokenAsync();
        var connection = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        await listener.WaitForStatusAsync(Connected);

        await DeleteTraccarTokenAsync();

        await listener.WaitForStatusAsync(NotConfigured);
        await connection.Disposed.WaitAsync(Timeout());
    }

    [Fact]
    public async Task Rejected_token_is_reported_and_retried_once_the_token_changes()
    {
        await using var listener = await ListenAsync();
        TraccarSocket.ThrowInstead = new TraccarCredentialRejectedException("rejected");
        await ConfigureTraccarTokenAsync();
        await listener.WaitForStatusAsync(CredentialRejected);

        TraccarSocket.ThrowInstead = null;
        await SetTraccarTokenAsync("corrected-token");

        var connection = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        connection.Token.ShouldBe("corrected-token");
    }

    [Fact]
    public async Task Dropped_connection_is_reported_and_reconnected()
    {
        await using var listener = await ListenAsync();
        await ConfigureTraccarTokenAsync();
        var first = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        await listener.WaitForStatusAsync(Connected);

        first.Drop();

        await listener.WaitForStatusAsync(Unavailable);
        var second = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        second.Token.ShouldBe("stored-token");
        await listener.WaitForStatusAsync(Connected);
    }

    [Fact]
    public async Task Removing_a_user_closes_only_their_live_connections()
    {
        var removedUser = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        await using var removedListener = await ListenAsync(removedUser);
        await using var remainingListener = await ListenAsync();
        var removedConnectionClosed = ClosedSignal(removedListener.Connection);
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.DeleteAsync(
            $"/api/admin/allowed-emails/{Uri.EscapeDataString(removedUser.Email)}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await removedConnectionClosed.WaitAsync(Timeout());
        remainingListener.Connection.State.ShouldBe(HubConnectionState.Connected);
    }

    private async Task SetTraccarTokenAsync(string token)
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.PutAsJsonAsync(
            "/api/credentials/traccar", new SetTraccarCredentialRequest(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task DeleteTraccarTokenAsync()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.DeleteAsync("/api/credentials/traccar", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<HubListener> ListenAsync(User? user = null)
    {
        user ??= await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        var listener = new HubListener(CreateHubConnection(user));
        await listener.Connection.StartAsync(TestContext.Current.CancellationToken);
        return listener;
    }

    private HubConnection CreateHubConnection(User? user, DateTimeOffset? signInExpiresAt = null, string? origin = null) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, DevicesHub.Route), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var webSocketClient = Factory.Server.CreateWebSocketClient();
                    webSocketClient.ConfigureRequest = request =>
                    {
                        if (origin is not null)
                        {
                            request.Headers.Origin = origin;
                        }

                        if (user is not null)
                        {
                            request.Headers[TestAuthDefaults.UserIdHeader] = user.Id.ToString();
                            request.Headers[TestAuthDefaults.EmailHeader] = user.Email;
                            request.Headers[TestAuthDefaults.NameHeader] = user.DisplayName;
                            request.Headers[TestAuthDefaults.RoleHeader] = user.Role.ToString();
                            if (signInExpiresAt is not null)
                            {
                                request.Headers[TestAuthDefaults.ExpiresAtHeader] = signInExpiresAt.Value.ToString("O");
                            }
                        }
                    };

                    return await webSocketClient.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .Build();

    private static Task ClosedSignal(HubConnection connection)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        return closed.Task;
    }

    private static CancellationToken Timeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout.Token;
    }

    private sealed class HubListener : IAsyncDisposable
    {
        public HubListener(HubConnection connection)
        {
            Connection = connection;
            connection.On<TraccarConnectionStatusResponse>(nameof(IDevicesClient.TraccarConnectionChanged), s => Statuses.Writer.TryWrite(s));
            connection.On<List<PositionResponse>>(nameof(IDevicesClient.PositionsUpdated), p => Positions.Writer.TryWrite(p));
            connection.On<List<DeviceStatusResponse>>(nameof(IDevicesClient.DevicesUpdated), d => Devices.Writer.TryWrite(d));
        }

        public HubConnection Connection { get; }
        public Channel<TraccarConnectionStatusResponse> Statuses { get; } = Channel.CreateUnbounded<TraccarConnectionStatusResponse>();
        public Channel<List<PositionResponse>> Positions { get; } = Channel.CreateUnbounded<List<PositionResponse>>();
        public Channel<List<DeviceStatusResponse>> Devices { get; } = Channel.CreateUnbounded<List<DeviceStatusResponse>>();

        public async Task WaitForStatusAsync(TraccarConnectionStatusResponse expected)
        {
            var cancellationToken = Timeout();
            while (await Statuses.Reader.ReadAsync(cancellationToken) != expected)
            {
            }
        }

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
