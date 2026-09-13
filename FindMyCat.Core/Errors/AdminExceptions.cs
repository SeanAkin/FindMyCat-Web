using System.Net;

namespace FindMyCat.Core.Errors;

public sealed class PrimaryAdministratorProtectedException : FindMyCatException
{
    private PrimaryAdministratorProtectedException(string message)
        : base(HttpStatusCode.Conflict, ErrorCodes.PrimaryAdministratorProtected, message)
    {
    }

    public static PrimaryAdministratorProtectedException CannotBeRemovedFromAllowList() =>
        new("The original administrator account's email cannot be removed from the allow-list.");

    public static PrimaryAdministratorProtectedException RoleCannotBeChanged() =>
        new("The original administrator account's role cannot be changed.");
}

public sealed class AllowedEmailNotFoundException(string email)
    : FindMyCatException(HttpStatusCode.NotFound, ErrorCodes.AllowedEmailNotFound, "That email is not on the allow-list.", logDetail: email);

public sealed class UserNotFoundException(Guid userId)
    : FindMyCatException(HttpStatusCode.NotFound, ErrorCodes.UserNotFound, "That user no longer exists.", logDetail: userId.ToString());
