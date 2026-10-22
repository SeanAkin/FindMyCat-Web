namespace FindMyCat.Core.Integrations.Traccar.Socket;

// Traccar sessions never expire unless web.sessionTimeout is set, so reconnects reuse one session
// instead of leaving a new one behind on every attempt.
public sealed class TraccarSessionStore
{
    private volatile TraccarSession? _current;

    internal TraccarSession? Current
    {
        get => _current;
        set => _current = value;
    }
}

internal sealed record TraccarSession(string Token, string Cookie);
