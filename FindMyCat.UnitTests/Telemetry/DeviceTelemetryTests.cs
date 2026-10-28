using FindMyCat.Core.Telemetry;

namespace FindMyCat.UnitTests.Telemetry;

public sealed class DeviceTelemetryTests
{
    [Fact]
    public void Parse_reads_a_network_report()
    {
        var result = DeviceTelemetry.Parse(
            DeviceTelemetry.NetworkKind, """{"command":"NET","status":"ok","mode":7,"edrx_s":81.92}""");

        var network = result.ShouldBeOfType<NetworkTelemetry>();
        network.Status.ShouldBe("ok");
        network.Mode.ShouldBe(7);
        network.EdrxSeconds.ShouldBe(81.92);
    }

    [Fact]
    public void Parse_reads_a_command_report()
    {
        var result = DeviceTelemetry.Parse(
            DeviceTelemetry.CommandKind,
            """{"command":"ping","status":"ok","reason":"user","method":"wifi","satellites":9,"elapsed_s":42,"battery":3.9,"uptime_s":123456}""");

        var command = result.ShouldBeOfType<CommandTelemetry>();
        command.Command.ShouldBe("ping");
        command.Status.ShouldBe("ok");
        command.Reason.ShouldBe("user");
        command.Method.ShouldBe("wifi");
        command.Satellites.ShouldBe(9);
        command.ElapsedSeconds.ShouldBe(42);
        command.Battery.ShouldBe(3.9);
        command.UptimeSeconds.ShouldBe(123456L);
    }

    [Fact]
    public void Parse_leaves_missing_fields_null()
    {
        var result = DeviceTelemetry.Parse(DeviceTelemetry.CommandKind, """{"command":"ping"}""");

        var command = result.ShouldBeOfType<CommandTelemetry>();
        command.Command.ShouldBe("ping");
        command.Status.ShouldBeNull();
        command.Satellites.ShouldBeNull();
        command.Battery.ShouldBeNull();
        command.UptimeSeconds.ShouldBeNull();
    }

    [Fact]
    public void Parse_ignores_unknown_fields()
    {
        var result = DeviceTelemetry.Parse(
            DeviceTelemetry.NetworkKind, """{"status":"ok","unexpected":{"nested":true},"extra":42}""");

        var network = result.ShouldBeOfType<NetworkTelemetry>();
        network.Status.ShouldBe("ok");
    }

    [Fact]
    public void Parse_tolerates_numeric_fields_sent_as_strings()
    {
        var result = DeviceTelemetry.Parse(
            DeviceTelemetry.CommandKind, """{"satellites":"9","battery":"3.9","uptime_s":"123456"}""");

        var command = result.ShouldBeOfType<CommandTelemetry>();
        command.Satellites.ShouldBe(9);
        command.Battery.ShouldBe(3.9);
        command.UptimeSeconds.ShouldBe(123456L);
    }

    [Fact]
    public void Parse_tolerates_fields_of_the_wrong_type()
    {
        var result = DeviceTelemetry.Parse(
            DeviceTelemetry.NetworkKind, """{"status":"ok","mode":"not-a-number","edrx_s":true}""");

        var network = result.ShouldBeOfType<NetworkTelemetry>();
        network.Status.ShouldBe("ok");
        network.Mode.ShouldBeNull();
        network.EdrxSeconds.ShouldBeNull();
    }

    [Fact]
    public void Parse_returns_null_for_an_unknown_kind()
    {
        DeviceTelemetry.Parse("wifi", """{"networks":[]}""").ShouldBeNull();
    }

    [Fact]
    public void Parse_returns_null_when_the_payload_is_not_an_object()
    {
        DeviceTelemetry.Parse(DeviceTelemetry.CommandKind, "[1,2,3]").ShouldBeNull();
    }

    [Fact]
    public void Parse_returns_null_for_invalid_json()
    {
        DeviceTelemetry.Parse(DeviceTelemetry.CommandKind, "not json").ShouldBeNull();
    }
}
