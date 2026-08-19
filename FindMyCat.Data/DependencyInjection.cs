using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyCat.Data;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddFindMyCatData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            options.UseSqlite(connectionString);
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAllowedEmailRepository, AllowedEmailRepository>();
        services.AddScoped<ISharedCredentialRepository, SharedCredentialRepository>();

        return services;
    }
}
