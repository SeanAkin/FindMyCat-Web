using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.Data.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken = default) =>
        db.Users.SingleOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(cancellationToken);

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTimeOffset loginTime, CancellationToken cancellationToken = default)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastLoginAt, loginTime), cancellationToken);
    }

    public async Task UpdateRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.Role, role), cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column, so sort client-side.
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        return users.OrderBy(u => u.CreatedAt).ToList();
    }
}
