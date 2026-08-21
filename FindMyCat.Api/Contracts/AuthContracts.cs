using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Services;

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
    [Required][EmailAddress][StringLength(320)] string Email,
    [Required][StringLength(255, MinimumLength = 1)] string DisplayName,
    [Required][StringLength(PasswordPolicy.MaximumLength)] string Password);

public sealed record LoginRequest(
    [Required][EmailAddress][StringLength(320)] string Email,
    [Required][StringLength(PasswordPolicy.MaximumLength)] string Password);

public sealed record AuthErrorResponse(string Code, string Message);
