using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;

namespace FindMyCat.Core.Services;

public interface IUserProvisioningService
{
    Task<UserProvisioningResult> ProvisionOrSignInAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken = default);
}

public sealed class UserProvisioningService(IUserRepository userRepository, IAllowedEmailRepository allowedEmailRepository) : IUserProvisioningService
{
    public async Task<UserProvisioningResult> ProvisionOrSignInAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken = default)
    {
        var existing = await userRepository.GetByGoogleSubjectIdAsync(googleUser.GoogleSubjectId, cancellationToken);
        if (existing is not null)
        {
            await userRepository.UpdateLastLoginAsync(existing.Id, DateTimeOffset.UtcNow, cancellationToken);
            return UserProvisioningResult.Success(existing);
        }
        
        // Firstuser = Primary Admin
        var anyUsersExist = await userRepository.AnyAsync(cancellationToken);
        if (!anyUsersExist)
        {
            var admin = await CreateUserAsync(googleUser, UserRole.Administrator, isPrimaryAdministrator: true, cancellationToken);
            return UserProvisioningResult.Success(admin);
        }

        var isAllowListed = await allowedEmailRepository.IsAllowedAsync(googleUser.Email, cancellationToken);
        if (!isAllowListed)
        {
            return UserProvisioningResult.Denied("This email has not been added to the allowed list.");
        }

        var user = await CreateUserAsync(googleUser, UserRole.User, isPrimaryAdministrator: false, cancellationToken);
        return UserProvisioningResult.Success(user);
    }

    private async Task<User> CreateUserAsync(GoogleUserInfo googleUser, UserRole role, bool isPrimaryAdministrator, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleSubjectId = googleUser.GoogleSubjectId,
            Email = googleUser.Email,
            DisplayName = googleUser.DisplayName,
            Role = role,
            IsPrimaryAdministrator = isPrimaryAdministrator,
            CreatedAt = now,
            LastLoginAt = now
        };

        return await userRepository.AddAsync(user, cancellationToken);
    }
}
