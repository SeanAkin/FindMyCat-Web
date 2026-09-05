using FindMyCat.Core.Entities;

namespace FindMyCat.Api.Contracts;

public sealed record AllowedEmailResponse(string Email, DateTimeOffset AddedAt)
{
    public static AllowedEmailResponse FromDomain(AllowedEmail email) => new(email.Email, email.AddedAt);
}

public sealed record AddAllowedEmailRequest(string Email);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsPrimaryAdministrator,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt)
{
    public static UserResponse FromDomain(User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role,
        user.IsPrimaryAdministrator,
        user.CreatedAt,
        user.LastLoginAt);
}

public sealed record UpdateUserRoleRequest(UserRole Role);

 