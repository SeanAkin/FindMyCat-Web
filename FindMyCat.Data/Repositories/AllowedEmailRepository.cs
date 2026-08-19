using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.Data.Repositories;

internal sealed class AllowedEmailRepository(AppDbContext db) : IAllowedEmailRepository
{
    public Task<bool> IsAllowedAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);
        return db.AllowedEmails.AnyAsync(a => a.Email == normalized, cancellationToken);
    }

    public async Task<AllowedEmail> AddAsync(string email, Guid addedByUserId, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);

        var existing = await db.AllowedEmails.SingleOrDefaultAsync(a => a.Email == normalized, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var entry = new AllowedEmail
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            AddedByUserId = addedByUserId,
            AddedAt = DateTimeOffset.UtcNow
        };

        db.AllowedEmails.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<bool> RemoveAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);

        var deleted = await db.AllowedEmails
            .Where(a => a.Email == normalized)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<IReadOnlyList<AllowedEmail>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.AllowedEmails.AsNoTracking().OrderBy(a => a.Email).ToListAsync(cancellationToken);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
