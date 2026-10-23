using FindMyCat.Core.Services;

namespace FindMyCat.Core.Integrations.Traccar.Socket;

public sealed class TraccarCredentialChangeSignal : ITraccarCredentialChangeNotifier
{
    private CancellationTokenSource _changed = new();

    public CancellationToken ChangedToken => Volatile.Read(ref _changed).Token;

    public void NotifyChanged() => Interlocked.Exchange(ref _changed, new CancellationTokenSource()).Cancel();
}
