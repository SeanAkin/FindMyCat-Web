using System.Net;

namespace FindMyCat.Core.Errors;

public abstract class FindMyCatException(
    HttpStatusCode status,
    string code,
    string clientSafeMessage,
    string? logDetail = null,
    Exception? innerException = null)
    : Exception(clientSafeMessage, innerException)
{
    public HttpStatusCode Status { get; } = status;

    public string Code { get; } = code;

    public string? LogDetail { get; } = logDetail;
}
