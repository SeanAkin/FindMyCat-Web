using FindMyCat.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

namespace FindMyCat.Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddFindMyCatAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection().SetApplicationName("FindMyCat");

        var googleAuthEnabled = configuration.GetValue("Authentication:Google:Enabled", true);
        services.AddSingleton(new GoogleAuthSettings(googleAuthEnabled));

        services.AddScoped<FindMyCatCookieAuthenticationEvents>();

        var authenticationBuilder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "findmycat.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.EventsType = typeof(FindMyCatCookieAuthenticationEvents);
            })
            .AddScheme<AuthenticationSchemeOptions, IngestApiKeyAuthenticationHandler>(
                IngestApiKeyDefaults.Scheme, _ => { });

        if (googleAuthEnabled)
        {
            services.AddScoped<GoogleAuthenticationEvents>();

            authenticationBuilder.AddGoogle(options =>
            {
                options.ClientId = configuration["Authentication:Google:ClientId"] ?? string.Empty;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
                options.CallbackPath = "/auth/callback";
                options.SaveTokens = false;
                options.EventsType = typeof(GoogleAuthenticationEvents);
            });
        }

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
