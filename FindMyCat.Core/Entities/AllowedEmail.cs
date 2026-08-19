namespace FindMyCat.Core.Entities;

public class AllowedEmail
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public Guid AddedByUserId { get; set; }

    public DateTimeOffset AddedAt { get; set; }
}
