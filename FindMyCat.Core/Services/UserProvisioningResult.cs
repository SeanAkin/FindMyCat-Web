using FindMyCat.Core.Entities;

namespace FindMyCat.Core.Services;

public sealed class UserProvisioningResult
{
    public User? User { get; }

    public string? DenialReason { get; }

    public bool IsSuccess => User is not null;

    private UserProvisioningResult(User? user, string? denialReason)
    {
        User = user;
        DenialReason = denialReason;
    }

    public static UserProvisioningResult Success(User user) => new(user, denialReason: null);

    public static UserProvisioningResult Denied(string reason) => new(user: null, reason);
}
