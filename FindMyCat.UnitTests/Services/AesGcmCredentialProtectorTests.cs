using System.Security.Cryptography;
using FindMyCat.Core.Services;

namespace FindMyCat.UnitTests.Services;

public class AesGcmCredentialProtectorTests
{
    private static AesGcmCredentialProtector NewProtector() =>
        new(RandomNumberGenerator.GetBytes(AesGcmCredentialProtector.KeySizeBytes));

    [Fact]
    public void Protect_then_Unprotect_round_trips_the_plaintext()
    {
        var protector = NewProtector();

        var cipher = protector.Encrypt("super-secret");

        cipher.ShouldNotBe("super-secret");
        protector.Decrypt(cipher).ShouldBe("super-secret");
    }

    [Fact]
    public void Protect_produces_distinct_ciphertext_for_the_same_plaintext()
    {
        var protector = NewProtector();

        // A fresh random nonce each time means identical inputs must not yield identical output.
        protector.Encrypt("same").ShouldNotBe(protector.Encrypt("same"));
    }

    [Fact]
    public void Unprotect_throws_when_the_key_does_not_match()
    {
        var cipher = NewProtector().Encrypt("secret");
        var differentKey = NewProtector();

        Should.Throw<CryptographicException>(() => differentKey.Decrypt(cipher));
    }

    [Fact]
    public void Unprotect_throws_on_foreign_or_legacy_ciphertext()
    {
        var protector = NewProtector();

        Should.Throw<CryptographicException>(() => protector.Decrypt("not-our-format"));
    }

    [Fact]
    public void Constructor_rejects_a_key_of_the_wrong_length()
    {
        Should.Throw<ArgumentException>(() => new AesGcmCredentialProtector(new byte[16]));
    }

    [Fact]
    public void ParseKey_accepts_a_base64_encoded_32_byte_key()
    {
        var raw = RandomNumberGenerator.GetBytes(AesGcmCredentialProtector.KeySizeBytes);

        var parsed = AesGcmCredentialProtector.ParseKey(Convert.ToBase64String(raw));

        parsed.ShouldBe(raw);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64!!!")]
    [InlineData("dG9vLXNob3J0")] // base64 of "too-short" — decodes to fewer than 32 bytes.
    public void ParseKey_rejects_missing_or_malformed_keys(string? value)
    {
        Should.Throw<InvalidOperationException>(() => AesGcmCredentialProtector.ParseKey(value));
    }
}
