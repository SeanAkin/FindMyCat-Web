namespace FindMyCat.Core.Integrations.Traccar.Socket;

// Traccar sessions never expire unless web.sessionTimeout is set. Logging in with the previous
// session cookie makes Traccar replace that session instead of leaving it behind.
public sealed class TraccarSessionStore
{
    private volatile string? _cookie;

    internal string? Cookie
    {
        get => _cookie;
        set => _cookie = value;
    }
}
