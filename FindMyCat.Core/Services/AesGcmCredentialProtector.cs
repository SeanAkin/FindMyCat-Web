using System.Security.Cryptography;
using System.Text;

namespace FindMyCat.Core.Services;

public interface ICredentialProtector
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

/// <summary>
/// Encrypts credential secrets with AES-256-GCM using a key the operator supplies via
/// configuration (the <c>FINDMYCAT_ENCRYPTION_KEY</c> environment variable). The key lives
/// outside the database, so a leaked DB or backup is useless without it, and a self-hoster
/// backs it up as a single string rather than an app-generated key directory.
/// </summary>
public sealed class AesGcmCredentialProtector : ICredentialProtector
{
    private const string Prefix = "fmc1:";
    
    public const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16; 

    private readonly byte[] _key;
    
    public AesGcmCredentialProtector(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"Encryption key must be {KeySizeBytes} bytes.", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aes = new AesGcm(_key, TagSizeBytes))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }
        
        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Ciphertext is not in the expected format.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Ciphertext is not valid base64.", ex);
        }

        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Ciphertext is too short to be valid.");
        }

        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes);
        var encrypted = payload.AsSpan(NonceSizeBytes + TagSizeBytes);
        var plaintextBytes = new byte[encrypted.Length];

        using (var aes = new AesGcm(_key, TagSizeBytes))
        {
            aes.Decrypt(nonce, encrypted, tag, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
    
    public static byte[] ParseKey(string? value)
    {
        const string howTo = "Generate one with `openssl rand -base64 32` and set it as the FINDMYCAT_ENCRYPTION_KEY environment variable.";

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"FINDMYCAT_ENCRYPTION_KEY is not set. {howTo}");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(value.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"FINDMYCAT_ENCRYPTION_KEY must be a base64 string. {howTo}", ex);
        }

        if (key.Length != KeySizeBytes)
        {
            throw new InvalidOperationException(
                $"FINDMYCAT_ENCRYPTION_KEY must decode to {KeySizeBytes} bytes (got {key.Length}). {howTo}");
        }

        return key;
    }
}
