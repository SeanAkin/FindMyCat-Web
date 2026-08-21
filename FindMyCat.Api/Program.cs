using System.Net;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FindMyCat.Api.Auth;
using FindMyCat.Api.Contracts;
using FindMyCat.Core;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Services;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using FindMyCat.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddFindMyCatCore();
builder.Services.AddFindMyCatData(builder.Configuration);


var credentialKey = AesGcmCredentialProtector.ParseKey(
    builder.Configuration["FINDMYCAT_ENCRYPTION_KEY"] is { } key && !string.IsNullOrWhiteSpace(key)
        ? key
        : throw new ArgumentException("Environment variable 'FINDMYCAT_ENCRYPTION_KEY' is missing or empty."));

builder.Services.AddSingleton<ICredentialProtector>(new AesGcmCredentialProtector(credentialKey));


var traccarBaseUrl = builder.Configuration["Traccar:BaseUrl"];
if (!Uri.TryCreate(traccarBaseUrl, UriKind.Absolute, out var traccarBaseUri))
{
    throw new InvalidOperationException(
        "Traccar:BaseUrl is missing or not an absolute URL (e.g. https://traccar.example.com).");
}

var traccarBaseAddressWithResolvableRelativePaths = new Uri(
    traccarBaseUri.AbsoluteUri.EndsWith('/') ? traccarBaseUri.AbsoluteUri : traccarBaseUri.AbsoluteUri + "/");

builder.Services.AddHttpClient<ITraccarClient, TraccarClient>(client =>
{
    client.BaseAddress = traccarBaseAddressWithResolvableRelativePaths;
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<TraccarTransportExceptionHandler>();

builder.Services.AddHttpClient<IHologramClient, HologramClient>(client =>
{
    client.BaseAddress = new Uri("https://dashboard.hologram.io/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<HologramTransportExceptionHandler>();


builder.Services.AddDataProtection().SetApplicationName("FindMyCat");

const string SignInDenialCodeItemsKey = "FindMyCat.SignInDenialCode";

builder.Services.AddAuthentication(options =>
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

        // This is an API: an unauthenticated/forbidden request should get a status code,
        // not a redirect to a login page that doesn't exist.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByIdAsync(parsedUserId, context.HttpContext.RequestAborted);
            if (user is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            if (!AuthClaimsFactory.MatchesUser(context.Principal!, user))
            {
                context.ReplacePrincipal(AuthClaimsFactory.CreatePrincipal(user));
                context.ShouldRenew = true;
            }
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/auth/callback";
        options.SaveTokens = false;

        // context.Fail is silently ignored by OAuthHandler.CreateTicketAsync; throwing is the
        // only way to abort ticket creation - protects the fix for the auth bypass from being
        // refactored away.
        options.Events.OnCreatingTicket = async context =>
        {
            var googleSubjectId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            var displayName = context.Principal?.FindFirstValue(ClaimTypes.Name) ?? email;

            if (string.IsNullOrWhiteSpace(googleSubjectId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
            {
                throw new AuthenticationException("Google did not return the expected profile information.");
            }

            var provisioningService = context.HttpContext.RequestServices.GetRequiredService<IUserProvisioningService>();
            var result = await provisioningService.ProvisionOrSignInAsync(
                new GoogleUserInfo(googleSubjectId, email, displayName),
                context.HttpContext.RequestAborted);

            if (!result.IsSuccess)
            {
                context.HttpContext.Items[SignInDenialCodeItemsKey] = result.DenialCode ?? "access_denied";
                throw new AuthenticationException(result.DenialReason ?? "Access denied.");
            }

            context.Principal = AuthClaimsFactory.CreatePrincipal(result.User!);
        };

        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            var code = context.HttpContext.Items[SignInDenialCodeItemsKey] as string ?? "sign_in_failed";
            context.Response.Redirect($"/login?error={code}");
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Everything requires an authenticated session unless the endpoint opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    TrustAnyUnroutablePrivateNetworkAsReverseProxy(options.KnownIPNetworks);
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            QueueLimit = 0
        }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new AuthErrorResponse("too_many_requests", "Too many attempts. Please wait a moment and try again."),
            cancellationToken);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

var hasRealClientIpAddresses = !app.Environment.IsEnvironment("Testing");
if (hasRealClientIpAddresses)
{
    app.UseRateLimiter();
}

app.MapControllers();

app.Run();

static void TrustAnyUnroutablePrivateNetworkAsReverseProxy(IList<System.Net.IPNetwork> knownNetworks)
{
    knownNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    knownNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    knownNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
}

// This is needed for WebApplicationFactory so IntegrationTests can se our entry point (https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0&pivots=xunit)
public partial class Program { }
