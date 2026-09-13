using System.Security.Claims;
using FindMyCat.Core.Entities;

namespace FindMyCat.Api.Contracts;

public sealed record SessionResponse(Guid Id, string Email, string DisplayName, string Role)
{
    public static SessionResponse FromUser(ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
        user.FindFirstValue(ClaimTypes.Email)!,
        user.FindFirstValue(ClaimTypes.Name)!,
        user.FindFirstValue(ClaimTypes.Role)!);

    public static SessionResponse FromDomain(User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role.ToString());
}

public sealed record RegisterRequest(
    string Email,
    string DisplayName,
    string Password);

public sealed record LoginRequest(
    string Email,
    string Password);


public enum AuthProvider
{
    Password = 0,
    Google = 1
}

public sealed record AuthProvidersResponse(IReadOnlyList<AuthProvider> Providers);
