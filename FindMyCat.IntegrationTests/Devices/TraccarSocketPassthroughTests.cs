using System.Net;
using System.Net.WebSockets;
using System.Text;
using FindMyCat.Api.Hubs;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class TraccarSocketPassthroughTests : IntegrationTestBase
{
    private const string TraccarMessage =
        """{"devices":[{"id":1,"name":"Mochi","uniqueId":"collar-1","status":"online","attributes":{"emoji":"🐈"}}]}""";

    public TraccarSocketPassthroughTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Unauthenticated_connection_is_rejected()
    {
        await Should.ThrowAsync<Exception>(() => ConnectAsync(user: null));
    }

    [Fact]
    public async Task Connection_from_another_origin_is_rejected()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<Exception>(() => ConnectAsync(user, origin: "https://other.example.com"));
    }

    [Fact]
    public async Task Traccar_messages_are_forwarded_unchanged()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var socket = await ConnectAsync(user);
        await ConfigureTraccarTokenAsync();
        var traccar = await TraccarSocket.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        var received = await ReceiveWhileSendingAsync(socket, () =>
            traccar.Send(new TraccarSocketMessage([], [], TraccarMessage)));

        received.ShouldBe(TraccarMessage);
    }

    [Fact]
    public async Task Removing_a_user_closes_their_socket()
    {
        var removedUser = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var socket = await ConnectAsync(removedUser);
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var response = await adminClient.DeleteAsync(
            $"/api/admin/allowed-emails/{Uri.EscapeDataString(removedUser.Email)}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitUntilClosedAsync(socket);
    }

    [Fact]
    public async Task Socket_closes_when_the_sign_in_expires()
    {
        var user = await CreateUserAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var socket = await ConnectAsync(user, signInExpiresAt: DateTimeOffset.UtcNow.AddSeconds(1));

        await WaitUntilClosedAsync(socket);
    }

    private async Task<WebSocket> ConnectAsync(User? user, string? origin = null, DateTimeOffset? signInExpiresAt = null)
    {
        var client = Factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
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

        var uri = new UriBuilder(Factory.Server.BaseAddress) { Scheme = "ws", Path = TraccarSocketPassthrough.Route }.Uri;
        return await client.ConnectAsync(uri, TestContext.Current.CancellationToken);
    }

    // The server registers a socket just after the handshake completes, so keep sending until one arrives.
    private static async Task<string> ReceiveWhileSendingAsync(WebSocket socket, Action send)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[4096];
        var receive = socket.ReceiveAsync(buffer, timeout.Token);
        while (!receive.IsCompleted)
        {
            send();
            await Task.WhenAny(receive, Task.Delay(100, timeout.Token));
        }

        var result = await receive;
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static async Task WaitUntilClosedAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
            }
        }
        catch (WebSocketException)
        {
        }
        catch (IOException)
        {
        }

        socket.State.ShouldNotBe(WebSocketState.Open);
    }
}