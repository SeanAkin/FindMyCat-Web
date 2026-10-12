using FindMyCat.Core.Integrations.Traccar.Socket;

namespace FindMyCat.UnitTests.Services.Traccar.Socket;

public class TraccarCredentialChangeSignalTests
{
    [Fact]
    public void NotifyChanged_cancels_the_current_token_and_issues_a_fresh_one()
    {
        var signal = new TraccarCredentialChangeSignal();
        var before = signal.ChangedToken;

        signal.NotifyChanged();

        before.IsCancellationRequested.ShouldBeTrue();
        signal.ChangedToken.IsCancellationRequested.ShouldBeFalse();
    }
}
