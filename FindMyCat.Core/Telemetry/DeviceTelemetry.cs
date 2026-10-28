using System.Globalization;
using System.Text.Json;

namespace FindMyCat.Core.Telemetry;

public sealed record NetworkTelemetry(string? Status, int? Mode, double? EdrxSeconds);

public sealed record CommandTelemetry(
    string? Command,
    string? Status,
    string? Reason,
    string? Method,
    int? Satellites,
    int? ElapsedSeconds,
    double? Battery,
    long? UptimeSeconds);

public static class DeviceTelemetry
{
    public const string NetworkKind = "network";
    public const string CommandKind = "command";

    public static object? Parse(string kind, string payloadJson)
    {
        if (kind is not (NetworkKind or CommandKind))
        {
            return null;
        }

        JsonElement body;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            body = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return kind switch
        {
            NetworkKind => new NetworkTelemetry(
                Text(body, "status"),
                Integer(body, "mode"),
                Real(body, "edrx_s")),
            CommandKind => new CommandTelemetry(
                Text(body, "command"),
                Text(body, "status"),
                Text(body, "reason"),
                Text(body, "method"),
                Integer(body, "satellites"),
                Integer(body, "elapsed_s"),
                Real(body, "battery"),
                Long(body, "uptime_s")),
            _ => null
        };
    }

    private static string? Text(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Integer(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
               && int.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? Long(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
               && long.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? Real(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
               && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
