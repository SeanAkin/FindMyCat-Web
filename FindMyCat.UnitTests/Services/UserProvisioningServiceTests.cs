using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Services;
using Moq;

namespace FindMyCat.UnitTests.Services;

public class UserProvisioningServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IAllowedEmailRepository> _allowedEmailRepository = new();
    private readonly UserProvisioningService _sut;

    public UserProvisioningServiceTests()
    {
        _sut = new UserProvisioningService(_userRepository.Object, _allowedEmailRepository.Object);
    }

    [Fact]
    public async Task ProvisionOrSignInAsync_ExistingUser_UpdatesLastLoginAndReturnsUser()
    {
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            GoogleSubjectId = "google-123",
            Email = "cat@example.com",
            DisplayName = "Cat Owner",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            LastLoginAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _userRepository
            .Setup(r => r.GetByGoogleSubjectIdAsync("google-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await _sut.ProvisionOrSignInAsync(new GoogleUserInfo("google-123", "cat@example.com", "Cat Owner"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.User.ShouldBe(existingUser);
        _userRepository.Verify(
            r => r.UpdateLastLoginAsync(existingUser.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProvisionOrSignInAsync_NoExistingUsers_CreatesFirstUserAsAdministratorRegardlessOfAllowList()
    {
        _userRepository
            .Setup(r => r.GetByGoogleSubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.AnyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepository
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await _sut.ProvisionOrSignInAsync(new GoogleUserInfo("google-1", "admin@example.com", "First User"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.User!.Role.ShouldBe(UserRole.Administrator);
        result.User!.IsPrimaryAdministrator.ShouldBeTrue();

        _allowedEmailRepository.Verify(r => r.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProvisionOrSignInAsync_AllowListed_CreatesStandardUser()
    {
        _userRepository
            .Setup(r => r.GetByGoogleSubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.AnyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _allowedEmailRepository
            .Setup(r => r.IsAllowedAsync("allowed@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepository
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await _sut.ProvisionOrSignInAsync(new GoogleUserInfo("google-3", "allowed@example.com", "Allowed Person"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.User!.Role.ShouldBe(UserRole.User);
        result.User!.IsPrimaryAdministrator.ShouldBeFalse();
    }

    [Fact]
    public async Task ProvisionOrSignInAsync_NotAllowListed_ReturnsDeniedWithoutCreatingUser()
    {
        _userRepository
            .Setup(r => r.GetByGoogleSubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.AnyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _allowedEmailRepository
            .Setup(r => r.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ProvisionOrSignInAsync(new GoogleUserInfo("google-4", "stranger@example.com", "Stranger"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.DenialReason.ShouldNotBeNullOrWhiteSpace();
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
