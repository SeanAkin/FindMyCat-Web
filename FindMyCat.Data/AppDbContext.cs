using FindMyCat.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<AllowedEmail> AllowedEmails => Set<AllowedEmail>();

    public DbSet<SharedCredential> SharedCredentials => Set<SharedCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
