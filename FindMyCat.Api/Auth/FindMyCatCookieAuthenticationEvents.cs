using System.Security.Claims;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FindMyCat.Api.Auth;

internal sealed class FindMyCatCookieAuthenticationEvents(IUserRepository userRepository) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            await RejectAsync(context);
            return;
        }

        var user = await userRepository.GetByIdAsync(parsedUserId, context.HttpContext.RequestAborted);
        if (user is null)
        {
            await RejectAsync(context);
            return;
        }

        if (!AuthClaimsFactory.MatchesUser(context.Principal!, user))
        {
            context.ReplacePrincipal(AuthClaimsFactory.CreatePrincipal(user));
            context.ShouldRenew = true;
        }
    }

    private static Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        return context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
