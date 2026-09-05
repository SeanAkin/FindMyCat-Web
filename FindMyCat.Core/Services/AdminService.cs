using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.RepositoryContracts;

namespace FindMyCat.Core.Services;

public interface IAdminService
{
    Task<IReadOnlyList<AllowedEmail>> ListAllowedEmailsAsync(CancellationToken cancellationToken = default);

    Task<AllowedEmail> AddAllowedEmailAsync(string email, Guid addedByUserId, CancellationToken cancellationToken = default);

    Task RemoveAllowedEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task SetUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
}

public sealed class AdminService(IAllowedEmailRepository allowedEmailRepository, IUserRepository userRepository) : IAdminService
{
    public Task<IReadOnlyList<AllowedEmail>> ListAllowedEmailsAsync(CancellationToken cancellationToken = default) =>
        allowedEmailRepository.ListAsync(cancellationToken);

    public Task<AllowedEmail> AddAllowedEmailAsync(string email, Guid addedByUserId, CancellationToken cancellationToken = default) =>
        allowedEmailRepository.AddAsync(email, addedByUserId, cancellationToken);

    public async Task RemoveAllowedEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is { IsPrimaryAdministrator: true })
        {
            throw PrimaryAdministratorProtectedException.CannotBeRemovedFromAllowList();
        }

        var removedPendingInvite = await allowedEmailRepository.RemoveAsync(email, cancellationToken);
        var removedActiveAccount = user is not null && await userRepository.DeleteAsync(user.Id, cancellationToken);

        if (!removedPendingInvite && !removedActiveAccount)
        {
            throw new AllowedEmailNotFoundException(email);
        }
    }

    public Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken = default) =>
        userRepository.ListAsync(cancellationToken);

    public async Task SetUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new UserNotFoundException(userId);
        }

        if (user.IsPrimaryAdministrator && role != UserRole.Administrator)
        {
            throw PrimaryAdministratorProtectedException.RoleCannotBeChanged();
        }

        await userRepository.UpdateRoleAsync(userId, role, cancellationToken);
    }
}
