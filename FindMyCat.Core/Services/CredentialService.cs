using System.Security.Cryptography;
using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services;

public interface ICredentialService
{
    Task<CredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task SetTraccarTokenAsync(string apiToken, CancellationToken cancellationToken = default);

    Task SetHologramKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteTraccarTokenAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteHologramKeyAsync(CancellationToken cancellationToken = default);
    
    Task<string?> GetTraccarTokenAsync(CancellationToken cancellationToken = default);

    Task<string?> GetHologramKeyAsync(CancellationToken cancellationToken = default);
}

public sealed class CredentialService(ISharedCredentialRepository credentialRepository, ICredentialProtector protector, ILogger<CredentialService> logger) : ICredentialService
{
    public async Task<CredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository.GetAsync(cancellationToken);
        if (credential is null)
        {
            return CredentialStatus.None;
        }

        return new CredentialStatus(
            credential.TraccarApiTokenProtected is not null,
            credential.HologramApiKeyProtected is not null);
    }

    public async Task SetTraccarTokenAsync(string apiToken, CancellationToken cancellationToken = default)
    {
        var credential = await GetOrCreateAsync(cancellationToken);
        credential.TraccarApiTokenProtected = protector.Encrypt(apiToken);
        await SaveAsync(credential, cancellationToken);
    }

    public async Task SetHologramKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var credential = await GetOrCreateAsync(cancellationToken);
        credential.HologramApiKeyProtected = protector.Encrypt(apiKey);
        await SaveAsync(credential, cancellationToken);
    }

    public async Task<bool> DeleteTraccarTokenAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository.GetAsync(cancellationToken);
        if (credential?.TraccarApiTokenProtected is null)
        {
            return false;
        }

        credential.TraccarApiTokenProtected = null;
        await SaveAsync(credential, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteHologramKeyAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository.GetAsync(cancellationToken);
        if (credential?.HologramApiKeyProtected is null)
        {
            return false;
        }

        credential.HologramApiKeyProtected = null;
        await SaveAsync(credential, cancellationToken);
        return true;
    }

    public async Task<string?> GetTraccarTokenAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository.GetAsync(cancellationToken);
        return Decrypt(credential?.TraccarApiTokenProtected, "Traccar API token");
    }

    public async Task<string?> GetHologramKeyAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository.GetAsync(cancellationToken);
        return Decrypt(credential?.HologramApiKeyProtected, "Hologram API key");
    }
    
    private string? Decrypt(string? ciphertext, string description)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return null;
        }

        try
        {
            return protector.Decrypt(ciphertext);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex,
                "Failed to decrypt stored {Credential}; the encryption key may be missing, rotated, or mismatched. Treating the credential as not configured.",
                description);
            return null;
        }
    }

    private async Task<SharedCredential> GetOrCreateAsync(CancellationToken cancellationToken) =>
        await credentialRepository.GetAsync(cancellationToken) ?? new SharedCredential { Id = SharedCredential.SingletonId, CreatedAt = DateTimeOffset.UtcNow };

    private Task SaveAsync(SharedCredential credential, CancellationToken cancellationToken)
    {
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        return credentialRepository.UpsertAsync(credential, cancellationToken);
    }
}
