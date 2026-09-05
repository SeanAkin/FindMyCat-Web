using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace FindMyCat.Api.Errors;

internal sealed class ValidationErrorFactory(IOptions<JsonOptions> jsonOptions)
{
    public const string RequestField = "_";

    private const string UnreadableValue = "The value provided is not valid.";

    private readonly JsonNamingPolicy? _namingPolicy =
        jsonOptions.Value.JsonSerializerOptions.PropertyNamingPolicy;

    public ApiError FromModelState(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Select(entry => (Field: FieldNameFor(entry.Key), Messages: MessagesFor(entry.Value)))
            .Where(entry => entry.Messages.Count > 0)
            .GroupBy(entry => entry.Field, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .SelectMany(entry => entry.Messages)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        return new ApiError(
            Code: null,
            Message: "The request was not valid.",
            Errors: errors.Count > 0 ? errors : null);
    }

    private static IReadOnlyList<string> MessagesFor(ModelStateEntry? entry) =>
        entry is null
            ? []
            : entry.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? UnreadableValue : error.ErrorMessage)
                .ToArray();
    
    private string FieldNameFor(string key)
    {
        var path = key.StartsWith('$') ? key[1..] : key;
        path = path.StartsWith('.') ? path[1..] : path;

        return path.Length == 0
            ? RequestField
            : string.Join('.', path.Split('.').Select(ConvertSegment));
    }
    
    private string ConvertSegment(string segment)
    {
        var indexer = segment.IndexOf('[', StringComparison.Ordinal);
        if (indexer < 0)
        {
            return ConvertName(segment);
        }

        var name = segment[..indexer];
        return name.Length == 0 ? segment : ConvertName(name) + segment[indexer..];
    }

    private string ConvertName(string name) =>
        name.Length == 0 || _namingPolicy is null ? name : _namingPolicy.ConvertName(name);
}
