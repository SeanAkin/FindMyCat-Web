using FindMyCat.Core.Models;

namespace FindMyCat.Api.Contracts;

// The vault is write-only from the client's perspective: this only ever reports
// whether each credential is configured, never the stored secret.
public sealed record CredentialStatusResponse(bool TraccarConfigured, bool HologramConfigured)
{
    public static CredentialStatusResponse FromDomain(CredentialStatus status) => new(
        status.TraccarConfigured,
        status.HologramConfigured);
}

public sealed record SetTraccarCredentialRequest(string ApiToken);

public sealed record SetHologramCredentialRequest(string ApiKey);
