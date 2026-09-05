using System.Net;

namespace FindMyCat.Core.Errors;

public abstract class FindMyCatException(
    HttpStatusCode status,
    string code,
    string clientSafeMessage,
    string? logDetail = null,
    Exception? innerException = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
    : Exception(clientSafeMessage, innerException)
{
    public HttpStatusCode Status { get; } = status;

    public string Code { get; } = code;

    public string? LogDetail { get; } = logDetail;

    /// <summary>
    /// Per-field messages for errors the caller can fix field by field. Null for everything else.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; } = errors;
}
