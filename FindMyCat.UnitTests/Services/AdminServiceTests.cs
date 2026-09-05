using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Services;
using Moq;

namespace FindMyCat.UnitTests.Services;

public class AdminServiceTests
{
    private readonly Mock<IAllowedEmailRepository> _allowedEmailRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        _sut = new AdminService(_allowedEmailRepository.Object, _userRepository.Object);
    }

    [Fact]
    public async Task AddAllowedEmailAsync_DelegatesToRepositoryWithAddedByUserId()
    {
        var addedByUserId = Guid.NewGuid();
        var expected = new AllowedEmail
        {
            Id = Guid.NewGuid(),
            Email = "friend@example.com",
            AddedByUserId = addedByUserId,
            AddedAt = DateTimeOffset.UtcNow
        };

        _allowedEmailRepository
            .Setup(r => r.AddAsync("friend@example.com", addedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.AddAllowedEmailAsync("friend@example.com", addedByUserId, TestContext.Current.CancellationToken);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task RemoveAllowedEmailAsync_ThrowsNotFound_WhenNothingWasRemoved()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _allowedEmailRepository
            .Setup(r => r.RemoveAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Should.ThrowAsync<AllowedEmailNotFoundException>(
            () => _sut.RemoveAllowedEmailAsync("missing@example.com", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAllowedEmailAsync_WithdrawsAPendingInvite_WhenNoAccountExists()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync("friend@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _allowedEmailRepository
            .Setup(r => r.RemoveAsync("friend@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Should.NotThrowAsync(
            () => _sut.RemoveAllowedEmailAsync("friend@example.com", TestContext.Current.CancellationToken));

        _allowedEmailRepository.Verify(
            r => r.RemoveAsync("friend@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]  // the invite row is still there and goes along with the account
    [InlineData(false)] // the invite row has already gone; the account must still be deleted
    public async Task RemoveAllowedEmailAsync_AlsoDeletesTheMatchingUserAccount(bool allowListRowRemoved)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "friend@example.com",
            DisplayName = "Friend",
            PasswordHash = "some-hash",
            Role = UserRole.User,
            IsPrimaryAdministrator = false
        };
        _userRepository
            .Setup(r => r.GetByEmailAsync("friend@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _allowedEmailRepository
            .Setup(r => r.RemoveAsync("friend@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowListRowRemoved);
        _userRepository
            .Setup(r => r.DeleteAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Deleting the account counts as a removal on its own, so this must not report the email
        // as missing even when there was no invite row left to remove.
        await Should.NotThrowAsync(
            () => _sut.RemoveAllowedEmailAsync("friend@example.com", TestContext.Current.CancellationToken));

        _userRepository.Verify(r => r.DeleteAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAllowedEmailAsync_RefusesToRemoveThePrimaryAdministratorsEmail()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync("founder@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                GoogleSubjectId = "google-0",
                Email = "founder@example.com",
                DisplayName = "Founder",
                Role = UserRole.Administrator,
                IsPrimaryAdministrator = true
            });

        await Should.ThrowAsync<PrimaryAdministratorProtectedException>(
            () => _sut.RemoveAllowedEmailAsync("founder@example.com", TestContext.Current.CancellationToken));

        _allowedEmailRepository.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetUserRoleAsync_ThrowsUserNotFound_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Should.ThrowAsync<UserNotFoundException>(
            () => _sut.SetUserRoleAsync(userId, UserRole.Administrator, TestContext.Current.CancellationToken));

        _userRepository.Verify(r => r.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetUserRoleAsync_PromotesStandardUserToAdministrator()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                GoogleSubjectId = "google-1",
                Email = "partner@example.com",
                DisplayName = "Partner",
                Role = UserRole.User,
                IsPrimaryAdministrator = false
            });

        await _sut.SetUserRoleAsync(userId, UserRole.Administrator, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.UpdateRoleAsync(userId, UserRole.Administrator, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserRoleAsync_DemotesNonPrimaryAdministrator()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                GoogleSubjectId = "google-2",
                Email = "promoted-admin@example.com",
                DisplayName = "Promoted Admin",
                Role = UserRole.Administrator,
                IsPrimaryAdministrator = false
            });

        await _sut.SetUserRoleAsync(userId, UserRole.User, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.UpdateRoleAsync(userId, UserRole.User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserRoleAsync_RefusesToDemoteThePrimaryAdministrator()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                GoogleSubjectId = "google-0",
                Email = "founder@example.com",
                DisplayName = "Founder",
                Role = UserRole.Administrator,
                IsPrimaryAdministrator = true
            });

        await Should.ThrowAsync<PrimaryAdministratorProtectedException>(
            () => _sut.SetUserRoleAsync(userId, UserRole.User, TestContext.Current.CancellationToken));

        _userRepository.Verify(r => r.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
