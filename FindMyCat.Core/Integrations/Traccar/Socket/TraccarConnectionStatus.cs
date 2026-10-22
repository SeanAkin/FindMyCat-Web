using FindMyCat.Core.Errors;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public sealed record TraccarConnectionStatus(bool Connected, string? ErrorCode)
{
    public static TraccarConnectionStatus Up { get; } = new(true, null);

    public static TraccarConnectionStatus NotConfigured { get; } = new(false, ErrorCodes.TraccarNotConfigured);

    public static TraccarConnectionStatus CredentialRejected { get; } = new(false, ErrorCodes.TraccarCredentialRejected);

    public static TraccarConnectionStatus Unavailable { get; } = new(false, ErrorCodes.TraccarUnavailable);

    public static TraccarConnectionStatus LiveUpdatesDisabled { get; } = new(false, ErrorCodes.LiveUpdatesDisabled);
}
