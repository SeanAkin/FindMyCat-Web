using System.Security.Cryptography;
using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FindMyCat.UnitTests.Services;

public class CredentialServiceTests
{
    private readonly Mock<ISharedCredentialRepository> _repository = new();
    private readonly AesGcmCredentialProtector _protector =
        new(RandomNumberGenerator.GetBytes(AesGcmCredentialProtector.KeySizeBytes));
    private readonly CredentialService _sut;

    public CredentialServiceTests()
    {
        _sut = new CredentialService(_repository.Object, _protector, NullLogger<CredentialService>.Instance);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNone_WhenNoRowExists()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedCredential?)null);

        var status = await _sut.GetStatusAsync(TestContext.Current.CancellationToken);

        status.TraccarConfigured.ShouldBeFalse();
        status.HologramConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_ReflectsWhichCredentialsArePresent()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedCredential
            {
                TraccarApiTokenProtected = _protector.Encrypt("token"),
                HologramApiKeyProtected = null
            });

        var status = await _sut.GetStatusAsync(TestContext.Current.CancellationToken);

        status.TraccarConfigured.ShouldBeTrue();
        status.HologramConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task SetTraccarTokenAsync_EncryptsBeforeStoring_WhenNoRowExists()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedCredential?)null);

        SharedCredential? saved = null;
        _repository.Setup(r => r.UpsertAsync(It.IsAny<SharedCredential>(), It.IsAny<CancellationToken>()))
            .Callback<SharedCredential, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        await _sut.SetTraccarTokenAsync("secret-token", TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.Id.ShouldBe(SharedCredential.SingletonId);
        saved.TraccarApiTokenProtected.ShouldNotBeNull();
        saved.TraccarApiTokenProtected.ShouldNotBe("secret-token");
        _protector.Decrypt(saved.TraccarApiTokenProtected).ShouldBe("secret-token");
    }

    [Fact]
    public async Task SetHologramKeyAsync_UpdatesExistingRow_WithoutTouchingTraccarToken()
    {
        var existingTraccar = _protector.Encrypt("existing-traccar");
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedCredential { TraccarApiTokenProtected = existingTraccar });

        SharedCredential? saved = null;
        _repository.Setup(r => r.UpsertAsync(It.IsAny<SharedCredential>(), It.IsAny<CancellationToken>()))
            .Callback<SharedCredential, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        await _sut.SetHologramKeyAsync("hologram-key", TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.TraccarApiTokenProtected.ShouldBe(existingTraccar);
        saved.HologramApiKeyProtected.ShouldNotBeNull();
        _protector.Decrypt(saved.HologramApiKeyProtected).ShouldBe("hologram-key");
    }

    [Fact]
    public async Task DeleteTraccarTokenAsync_ReturnsFalse_WhenNothingConfigured()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedCredential?)null);

        var removed = await _sut.DeleteTraccarTokenAsync(TestContext.Current.CancellationToken);

        removed.ShouldBeFalse();
        _repository.Verify(r => r.UpsertAsync(It.IsAny<SharedCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteHologramKeyAsync_ClearsOnlyHologram_AndReturnsTrue()
    {
        var traccar = _protector.Encrypt("traccar");
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedCredential
            {
                TraccarApiTokenProtected = traccar,
                HologramApiKeyProtected = _protector.Encrypt("hologram")
            });

        SharedCredential? saved = null;
        _repository.Setup(r => r.UpsertAsync(It.IsAny<SharedCredential>(), It.IsAny<CancellationToken>()))
            .Callback<SharedCredential, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var removed = await _sut.DeleteHologramKeyAsync(TestContext.Current.CancellationToken);

        removed.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved.HologramApiKeyProtected.ShouldBeNull();
        saved.TraccarApiTokenProtected.ShouldBe(traccar);
    }

    [Fact]
    public async Task GetTraccarTokenAsync_DecryptsStoredCiphertext()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedCredential { TraccarApiTokenProtected = _protector.Encrypt("plaintext-token") });

        var token = await _sut.GetTraccarTokenAsync(TestContext.Current.CancellationToken);

        token.ShouldBe("plaintext-token");
    }

    [Fact]
    public async Task GetHologramKeyAsync_ReturnsNull_WhenNotConfigured()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedCredential());

        var key = await _sut.GetHologramKeyAsync(TestContext.Current.CancellationToken);

        key.ShouldBeNull();
    }

    [Fact]
    public async Task GetTraccarTokenAsync_ReturnsNull_WhenKeyCannotDecryptCiphertext()
    {
        var otherKeyProtector = new AesGcmCredentialProtector(
            RandomNumberGenerator.GetBytes(AesGcmCredentialProtector.KeySizeBytes));
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedCredential
            {
                TraccarApiTokenProtected = otherKeyProtector.Encrypt("token-from-old-key")
            });

        var token = await _sut.GetTraccarTokenAsync(TestContext.Current.CancellationToken);

        token.ShouldBeNull();
    }
}
