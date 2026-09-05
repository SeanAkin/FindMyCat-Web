namespace FindMyCat.Core.Errors;

/// <summary>
/// Per-field validation messages, keyed by the request field they belong to.
/// </summary>
public static class ValidationErrors
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> For(string field, IReadOnlyList<string> messages) 
        => new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { [field] = messages };
}
