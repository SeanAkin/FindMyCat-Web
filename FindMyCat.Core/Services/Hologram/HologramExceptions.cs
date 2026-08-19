namespace FindMyCat.Core.Services.Hologram;

public sealed class HologramNotConfiguredException() : Exception("No Hologram API key is configured.");

public sealed class HologramDeviceNotFoundException(string detail)
    : Exception($"Could not resolve a Hologram device to command: {detail}.");

public sealed class HologramUpstreamException : Exception
{
    public bool CredentialRejected { get; }

    public HologramUpstreamException(string message, bool credentialRejected, Exception? innerException = null)
        : base(message, innerException)
    {
        CredentialRejected = credentialRejected;
    }
}
