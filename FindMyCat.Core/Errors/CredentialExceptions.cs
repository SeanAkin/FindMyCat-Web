using System.Net;

namespace FindMyCat.Core.Errors;

public sealed class CredentialNotConfiguredException(string credential)
    : FindMyCatException(HttpStatusCode.NotFound, ErrorCodes.CredentialNotConfigured,
        "That credential is not configured.", logDetail: credential);
