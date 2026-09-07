using System.Net;

namespace FindMyCat.Core.Errors;

public sealed class NotAllowListedException()
    : FindMyCatException(HttpStatusCode.Forbidden, ErrorCodes.NotAllowListed,
        "This email has not been added to the allowed list.");

public sealed class InvalidCredentialsException()
    : FindMyCatException(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials,
        "Incorrect email or password.");

public sealed class EmailAlreadyRegisteredException()
    : FindMyCatException(HttpStatusCode.Conflict, ErrorCodes.EmailAlreadyRegistered,
        "An account with this email already exists.");

public sealed class EmailRegisteredWithPasswordException()
    : FindMyCatException(HttpStatusCode.Conflict, ErrorCodes.EmailRegisteredWithPassword,
        "This email already has a password-based account. Sign in with your email and password instead.");

public sealed class WeakPasswordException(IReadOnlyList<string> violations)
    : FindMyCatException(HttpStatusCode.BadRequest, ErrorCodes.WeakPassword,
        "That password does not meet the requirements.",
        logDetail: string.Join(" ", violations));
