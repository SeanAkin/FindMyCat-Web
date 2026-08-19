using System.Security.Claims;
using System.Text.Json.Serialization;
using FindMyCat.Core;
using FindMyCat.Core.Services;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using FindMyCat.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
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
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/auth/callback";
        options.SaveTokens = false;

        options.Events.OnCreatingTicket = async context =>
        {
            var googleSubjectId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            var displayName = context.Principal?.FindFirstValue(ClaimTypes.Name) ?? email;

            if (string.IsNullOrWhiteSpace(googleSubjectId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
            {
                context.Fail("Google did not return the expected profile information.");
                return;
            }

            var provisioningService = context.HttpContext.RequestServices.GetRequiredService<IUserProvisioningService>();
            var result = await provisioningService.ProvisionOrSignInAsync(
                new GoogleUserInfo(googleSubjectId, email, displayName),
                context.HttpContext.RequestAborted);

            if (!result.IsSuccess)
            {
                context.HttpContext.Items[SignInDenialCodeItemsKey] = "access_denied";
                context.Fail(result.DenialReason ?? "Access denied.");
                return;
            }

            // Replace Google's claim set with a minimal one describing our own local user,
            // since that's what ends up persisted in the app's cookie session.
            var user = result.User!;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Random Change to Test PR Checks 

// This is needed for WebApplicationFactory so IntegrationTests can se our entry point (https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0&pivots=xunit)
public partial class Program { }
