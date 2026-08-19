namespace FindMyCat.Core.Entities;

public class SharedCredential
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    
    public string? TraccarApiTokenProtected { get; set; }

    public string? HologramApiKeyProtected { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
