using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FindMyCat.UnitTests.Services;

public class UserProvisioningServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IAllowedEmailRepository> _allowedEmailRepository = new();
    private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
    private readonly UserProvisioningService _sut;

    public UserProvisioningServiceTests()
    {
        _sut = new UserProvisioningService(_userRepository.Object, _allowedEmailRepository.Object, _passwordHasher);
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
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task ProvisionOrSignInAsync_EmailAlreadyHasPasswordAccount_ReturnsDeniedWithoutCreatingDuplicate()
    {
        var existingPasswordUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "cat@example.com",
            DisplayName = "Cat Owner",
            PasswordHash = "some-hash",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };

        _userRepository
            .Setup(r => r.GetByGoogleSubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailAsync("cat@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPasswordUser);

        var result = await _sut.ProvisionOrSignInAsync(new GoogleUserInfo("google-5", "cat@example.com", "Cat Owner"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.DenialCode.ShouldBe("email_registered_with_password");
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterWithPasswordAsync_WeakPassword_ReturnsDeniedWithoutCreatingUser()
    {
        var result = await _sut.RegisterWithPasswordAsync(
            "new@example.com", "New Person", "weak", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.DenialCode.ShouldBe("weak_password");
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterWithPasswordAsync_EmailAlreadyRegistered_ReturnsDeniedWithoutCreatingUser()
    {
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            GoogleSubjectId = "google-1",
            Email = "cat@example.com",
            DisplayName = "Cat Owner",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        _userRepository
            .Setup(r => r.GetByEmailAsync("cat@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await _sut.RegisterWithPasswordAsync(
            "cat@example.com", "Cat Owner", "Str0ng!Pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.DenialCode.ShouldBe("email_already_registered");
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterWithPasswordAsync_NoExistingUsers_CreatesFirstUserAsAdministratorRegardlessOfAllowList()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.AnyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepository
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await _sut.RegisterWithPasswordAsync(
            "admin@example.com", "First User", "Str0ng!Pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.User!.Role.ShouldBe(UserRole.Administrator);
        result.User!.IsPrimaryAdministrator.ShouldBeTrue();
        result.User!.GoogleSubjectId.ShouldBeNull();
        result.User!.PasswordHash.ShouldNotBeNullOrWhiteSpace();
        _allowedEmailRepository.Verify(r => r.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterWithPasswordAsync_NotAllowListed_ReturnsDeniedWithoutCreatingUser()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.AnyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _allowedEmailRepository
            .Setup(r => r.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.RegisterWithPasswordAsync(
            "stranger@example.com", "Stranger", "Str0ng!Pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.DenialCode.ShouldBe("not_allow_listed");
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterWithPasswordAsync_AllowListed_CreatesStandardUserWithHashedPassword()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        var result = await _sut.RegisterWithPasswordAsync(
            "allowed@example.com", "Allowed Person", "Str0ng!Pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.User!.Role.ShouldBe(UserRole.User);
        result.User!.IsPrimaryAdministrator.ShouldBeFalse();
        result.User!.PasswordHash.ShouldNotBe("Str0ng!Pass");
        _passwordHasher.VerifyHashedPassword(result.User!, result.User!.PasswordHash!, "Str0ng!Pass")
            .ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task SignInWithPasswordAsync_CorrectPassword_SucceedsAndUpdatesLastLogin()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "cat@example.com",
            DisplayName = "Cat Owner",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            LastLoginAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, "Str0ng!Pass");

        _userRepository
            .Setup(r => r.GetByEmailAsync("cat@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.SignInWithPasswordAsync("cat@example.com", "Str0ng!Pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.User.ShouldBe(user);
        _userRepository.Verify(
            r => r.UpdateLastLoginAsync(user.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignInWithPasswordAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "cat@example.com",
            DisplayName = "Cat Owner",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, "Str0ng!Pass");

        _userRepository
            .Setup(r => r.GetByEmailAsync("cat@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.SignInWithPasswordAsync("cat@example.com", "wrong-password", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        _userRepository.Verify(
            r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SignInWithPasswordAsync_UnknownEmail_ReturnsInvalidCredentials()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync("nobody@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.SignInWithPasswordAsync("nobody@example.com", "Str0ng!Pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task SignInWithPasswordAsync_GoogleOnlyAccount_ReturnsInvalidCredentials()
    {
        var googleOnlyUser = new User
        {
            Id = Guid.NewGuid(),
            GoogleSubjectId = "google-1",
            Email = "cat@example.com",
            DisplayName = "Cat Owner",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };

        _userRepository
            .Setup(r => r.GetByEmailAsync("cat@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(googleOnlyUser);

        var result = await _sut.SignInWithPasswordAsync("cat@example.com", "any-password", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
    }
}
