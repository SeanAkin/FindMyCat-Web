using System.Text.Json.Serialization;

namespace FindMyCat.Api.Errors;

/// <param name="Code">
/// A domain-specific code the client can branch on, or null when the HTTP status says everything
/// there is to say. Omitted from the payload when null.
/// </param>
/// <param name="Message">A client-safe sentence. Always present.</param>
/// <param name="Errors">
/// Per-field messages keyed by camelCased request field. Omitted unless the request failed validation.
/// </param>
public sealed record ApiError(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors = null);
