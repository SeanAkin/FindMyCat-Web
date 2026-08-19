using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;

namespace FindMyCat.Core.Services;

public interface IAdminService
{
    Task<IReadOnlyList<AllowedEmail>> ListAllowedEmailsAsync(CancellationToken cancellationToken = default);

    Task<AllowedEmail> AddAllowedEmailAsync(string email, Guid addedByUserId, CancellationToken cancellationToken = default);

    Task<RemoveAllowedEmailResult> RemoveAllowedEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<SetUserRoleResult> SetUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
}

public sealed class AdminService(IAllowedEmailRepository allowedEmailRepository, IUserRepository userRepository) : IAdminService
{
    public Task<IReadOnlyList<AllowedEmail>> ListAllowedEmailsAsync(CancellationToken cancellationToken = default) =>
        allowedEmailRepository.ListAsync(cancellationToken);

    public Task<AllowedEmail> AddAllowedEmailAsync(string email, Guid addedByUserId, CancellationToken cancellationToken = default) =>
        allowedEmailRepository.AddAsync(email, addedByUserId, cancellationToken);

    public async Task<RemoveAllowedEmailResult> RemoveAllowedEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is { IsPrimaryAdministrator: true })
        {
            return RemoveAllowedEmailResult.PrimaryAdministratorProtected;
        }

        var removed = await allowedEmailRepository.RemoveAsync(email, cancellationToken);
        return removed ? RemoveAllowedEmailResult.Removed : RemoveAllowedEmailResult.NotFound;
    }

    public Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken = default) =>
        userRepository.ListAsync(cancellationToken);

    public async Task<SetUserRoleResult> SetUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return SetUserRoleResult.UserNotFound;
        }

        if (user.IsPrimaryAdministrator && role != UserRole.Administrator)
        {
            return SetUserRoleResult.PrimaryAdministratorProtected;
        }

        await userRepository.UpdateRoleAsync(userId, role, cancellationToken);
        return SetUserRoleResult.Success;
    }
}
