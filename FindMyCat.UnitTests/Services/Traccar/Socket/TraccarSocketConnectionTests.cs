using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar.Socket;
using FindMyCat.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarSocketConnectionTests
{
    private const string PositionJson =
        """{"deviceId":1,"fixTime":"2026-01-01T00:00:00Z","deviceTime":"2026-01-01T00:00:00Z","serverTime":"2026-01-01T00:00:00Z","valid":true,"latitude":1.5,"longitude":2.5,"altitude":0,"speed":0,"course":0,"accuracy":5}""";

    private static async Task<List<TraccarSocketMessage>> ReadAllAsync(ScriptedWebSocket socket)
    {
        await using var connection = new TraccarSocketConnection(socket, NullLogger.Instance);
        var messages = new List<TraccarSocketMessage>();
        await foreach (var message in connection.ReadMessagesAsync(TestContext.Current.CancellationToken))
        {
            messages.Add(message);
        }

        return messages;
    }

    [Fact]
    public async Task ReadMessages_assembles_a_message_split_across_frames()
    {
        var socket = new ScriptedWebSocket()
            .TextFrame("""{"positions":[""", endOfMessage: false)
            .TextFrame(PositionJson, endOfMessage: false)
            .TextFrame("]}")
            .CloseFrame();

        var messages = await ReadAllAsync(socket);

        messages.ShouldHaveSingleItem().Positions.ShouldHaveSingleItem().Latitude.ShouldBe(1.5);
    }

    [Fact]
    public async Task ReadMessages_skips_keepalives()
    {
        var socket = new ScriptedWebSocket()
            .TextFrame("{}")
            .TextFrame($$"""{"positions":[{{PositionJson}}]}""")
            .TextFrame("{}")
            .CloseFrame();

        var messages = await ReadAllAsync(socket);

        messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReadMessages_skips_a_message_that_cannot_be_parsed_and_keeps_reading()
    {
        var socket = new ScriptedWebSocket()
            .TextFrame("""{"positions":[{"deviceId":"not-a-number"}]}""")
            .TextFrame($$"""{"positions":[{{PositionJson}}]}""")
            .CloseFrame();

        var messages = await ReadAllAsync(socket);

        messages.ShouldHaveSingleItem().Positions.ShouldHaveSingleItem().Latitude.ShouldBe(1.5);
    }

    [Fact]
    public async Task ReadMessages_ends_when_traccar_closes_the_socket()
    {
        var socket = new ScriptedWebSocket().CloseFrame();

        var messages = await ReadAllAsync(socket);

        messages.ShouldBeEmpty();
        socket.CloseSent.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadMessages_throws_unavailable_when_the_connection_is_lost()
    {
        var socket = new ScriptedWebSocket().ConnectionLost();

        await Should.ThrowAsync<TraccarUnavailableException>(() => ReadAllAsync(socket));
    }
}
