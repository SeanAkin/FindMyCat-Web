using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.Data.Repositories;

internal sealed class SharedCredentialRepository(AppDbContext db) : ISharedCredentialRepository
{
    public Task<SharedCredential?> GetAsync(CancellationToken cancellationToken = default) =>
        db.SharedCredentials.AsNoTracking().SingleOrDefaultAsync(c => c.Id == SharedCredential.SingletonId, cancellationToken);

    public async Task UpsertAsync(SharedCredential credential, CancellationToken cancellationToken = default)
    {
        var exists = await db.SharedCredentials.AnyAsync(c => c.Id == SharedCredential.SingletonId, cancellationToken);
        if (exists)
        {
            db.SharedCredentials.Update(credential);
        }
        else
        {
            db.SharedCredentials.Add(credential);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
