using FindMyCat.Core.Entities;

namespace FindMyCat.Core.RepositoryContracts;

public interface IAllowedEmailRepository
{
    Task<bool> IsAllowedAsync(string email, CancellationToken cancellationToken = default);

    Task<AllowedEmail> AddAsync(string email, Guid addedByUserId, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllowedEmail>> ListAsync(CancellationToken cancellationToken = default);
}
