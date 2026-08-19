using System.Security.Claims;

namespace FindMyCat.Api.Contracts;

public sealed record SessionResponse(Guid Id, string Email, string DisplayName, string Role)
{
    public static SessionResponse FromUser(ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
        user.FindFirstValue(ClaimTypes.Email)!,
        user.FindFirstValue(ClaimTypes.Name)!,
        user.FindFirstValue(ClaimTypes.Role)!);
}
