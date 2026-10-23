using System.Text;
using System.Text.Json;
using FindMyCat.Core.Integrations.Traccar.Socket;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarSocketMessageTests
{
    private const string PositionsMessage = """
        {"positions":[{"id":1001,"attributes":{"sat":7.0,"batteryLevel":80.0,"distance":1.5,"totalDistance":1000.0,"motion":false},"deviceId":1,"protocol":"osmand","serverTime":"2026-01-01T12:00:00.000+00:00","deviceTime":"2026-01-01T12:00:00.000+00:00","fixTime":"2026-01-01T12:00:00.000+00:00","valid":true,"latitude":12.345678,"longitude":-65.4321,"altitude":0.0,"speed":0.0,"course":0.0,"address":null,"accuracy":10.5,"network":null,"geofenceIds":null}]}
        """;

    private const string DevicesMessage = """
        {"devices":[{"id":1,"attributes":{},"groupId":0,"calendarId":0,"name":"Test Collar","uniqueId":"unique-1","status":"online","lastUpdate":"2026-01-01T12:00:00.000+00:00","positionId":1001,"phone":null,"model":null,"contact":null,"category":null,"disabled":false,"expirationTime":null}]}
        """;

    private static TraccarSocketMessage Parse(string json) => TraccarSocketMessage.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Parse_maps_positions()
    {
        var message = Parse(PositionsMessage);

        message.Devices.ShouldBeEmpty();
        var position = message.Positions.ShouldHaveSingleItem();
        position.DeviceId.ShouldBe(1);
        position.Latitude.ShouldBe(12.345678);
        position.Longitude.ShouldBe(-65.4321);
        position.Accuracy.ShouldBe(10.5);
        position.BatteryLevel.ShouldBe(80.0);
        position.Satellites.ShouldBe(7.0);
    }

    [Fact]
    public void Parse_maps_devices_without_a_position()
    {
        var message = Parse(DevicesMessage);

        message.Positions.ShouldBeEmpty();
        var device = message.Devices.ShouldHaveSingleItem();
        device.Id.ShouldBe(1);
        device.Name.ShouldBe("Test Collar");
        device.UniqueId.ShouldBe("unique-1");
        device.Status.ShouldBe("online");
        device.LatestPosition.ShouldBeNull();
    }

    [Fact]
    public void Parse_treats_keepalive_as_empty()
    {
        Parse("{}").IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Parse_ignores_events()
    {
        Parse("""{"events":[{"id":1,"type":"deviceOnline","deviceId":2}]}""").IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Parse_throws_for_malformed_json()
    {
        Should.Throw<JsonException>(() => Parse("not json"));
    }
}
