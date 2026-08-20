namespace FindMyCat.Core.Entities;

public class User
{
    public Guid Id { get; set; }

    public string? GoogleSubjectId { get; set; }

    public string? PasswordHash { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public UserRole Role { get; set; }

    public bool IsPrimaryAdministrator { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastLoginAt { get; set; }
}
