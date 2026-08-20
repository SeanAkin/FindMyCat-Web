using FindMyCat.Core.Entities;
using FindMyCat.Core.Services;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyCat.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddFindMyCatCore(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICredentialService, CredentialService>();
        services.AddScoped<ITraccarService, TraccarService>();
        services.AddScoped<IHologramService, HologramService>();
        services.AddTransient<TraccarTransportExceptionHandler>();
        services.AddTransient<HologramTransportExceptionHandler>();

        return services;
    }
}
