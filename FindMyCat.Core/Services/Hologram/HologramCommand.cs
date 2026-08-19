namespace FindMyCat.Core.Services.Hologram;

public enum HologramCommand
{
    Ping,
    Lost,
    Active
}

public static class HologramCommandExtensions
{
    public static string ToStringValue(this HologramCommand command) => command switch
    {
        HologramCommand.Ping => "ping",
        HologramCommand.Lost => "lost",
        HologramCommand.Active => "active",
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
    };
}
