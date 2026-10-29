namespace FindMyCat.Core.Integrations.Hologram;

public enum HologramCommand
{
    Ping,
    PingWifi,
    PingGps,
    SpoofPing,
    Lost,
    Active,
    Idle,
    Update
}

public static class HologramCommandExtensions
{
    public static string ToStringValue(this HologramCommand command) => command switch
    {
        HologramCommand.Ping => "ping",
        HologramCommand.PingWifi => "ping-wifi",
        HologramCommand.PingGps => "ping-gps",
        HologramCommand.SpoofPing => "spoof-ping",
        HologramCommand.Lost => "lost",
        HologramCommand.Active => "active",
        HologramCommand.Idle => "idle",
        HologramCommand.Update => "update",
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
    };

    public static bool TryParse(string value, out HologramCommand command)
    {
        foreach (var candidate in Enum.GetValues<HologramCommand>())
        {
            if (candidate.ToStringValue() == value)
            {
                command = candidate;
                return true;
            }
        }

        command = default;
        return false;
    }
}