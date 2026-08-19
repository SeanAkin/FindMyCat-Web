using FindMyCat.Core.Entities;

namespace FindMyCat.Core.RepositoryContracts;

public interface ISharedCredentialRepository
{
    Task<SharedCredential?> GetAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(SharedCredential credential, CancellationToken cancellationToken = default);
}
