using FindMyCat.Core.Entities;

namespace FindMyCat.Core.Services;

public sealed class UserProvisioningResult
{
    public User? User { get; }

    public string? DenialReason { get; }

    public string? DenialCode { get; }

    public bool IsSuccess => User is not null;

    private UserProvisioningResult(User? user, string? denialReason, string? denialCode)
    {
        User = user;
        DenialReason = denialReason;
        DenialCode = denialCode;
    }

    public static UserProvisioningResult Success(User user) => new(user, denialReason: null, denialCode: null);

    public static UserProvisioningResult Denied(string reason, string code = "not_allow_listed") =>
        new(user: null, reason, code);
}
