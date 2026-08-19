namespace FindMyCat.Core.Services.Traccar;

public sealed class TraccarNotConfiguredException() : Exception("No Traccar token is configured for this user.");

public sealed class TraccarUpstreamException : Exception
{
    public bool CredentialRejected { get; }

    public TraccarUpstreamException(string message, bool credentialRejected, Exception? innerException = null)
        : base(message, innerException)
    {
        CredentialRejected = credentialRejected;
    }
}
