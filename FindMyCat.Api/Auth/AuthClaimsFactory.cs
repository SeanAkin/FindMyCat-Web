using System.Security.Claims;
using FindMyCat.Core.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FindMyCat.Api.Auth;

public static class AuthClaimsFactory
{
    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static bool MatchesUser(ClaimsPrincipal principal, User user) =>
        principal.FindFirstValue(ClaimTypes.Email) == user.Email
        && principal.FindFirstValue(ClaimTypes.Name) == user.DisplayName
        && principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString();
}
