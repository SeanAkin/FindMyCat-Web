using FindMyCat.Core.Entities;
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
    public async Task RemoveAllowedEmailAsync_ReturnsNotFound_WhenNothingWasRemoved()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _allowedEmailRepository
            .Setup(r => r.RemoveAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.RemoveAllowedEmailAsync("missing@example.com", TestContext.Current.CancellationToken);

        result.ShouldBe(RemoveAllowedEmailResult.NotFound);
    }

    [Fact]
    public async Task RemoveAllowedEmailAsync_ReturnsRemoved_WhenSomethingWasRemoved()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync("friend@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _allowedEmailRepository
            .Setup(r => r.RemoveAsync("friend@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.RemoveAllowedEmailAsync("friend@example.com", TestContext.Current.CancellationToken);

        result.ShouldBe(RemoveAllowedEmailResult.Removed);
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

        var result = await _sut.RemoveAllowedEmailAsync("founder@example.com", TestContext.Current.CancellationToken);

        result.ShouldBe(RemoveAllowedEmailResult.PrimaryAdministratorProtected);
        _allowedEmailRepository.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetUserRoleAsync_ReturnsUserNotFound_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.SetUserRoleAsync(userId, UserRole.Administrator, TestContext.Current.CancellationToken);

        result.ShouldBe(SetUserRoleResult.UserNotFound);
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

        var result = await _sut.SetUserRoleAsync(userId, UserRole.Administrator, TestContext.Current.CancellationToken);

        result.ShouldBe(SetUserRoleResult.Success);
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

        var result = await _sut.SetUserRoleAsync(userId, UserRole.User, TestContext.Current.CancellationToken);

        result.ShouldBe(SetUserRoleResult.Success);
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

        var result = await _sut.SetUserRoleAsync(userId, UserRole.User, TestContext.Current.CancellationToken);

        result.ShouldBe(SetUserRoleResult.PrimaryAdministratorProtected);
        _userRepository.Verify(r => r.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
