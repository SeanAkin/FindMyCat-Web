namespace FindMyCat.Core.Models;

/// <summary>
/// Whether each external credential is configured for a user. Never carries the
/// secret itself — the vault is write-only from the client's perspective.
/// </summary>
public sealed record CredentialStatus(bool TraccarConfigured, bool HologramConfigured)
{
    public static readonly CredentialStatus None = new(false, false);
}
