using FindMyCat.Core.Errors;
using FluentValidation.Results;

namespace FindMyCat.Api.Errors;

internal static class ValidationErrorFactory
{
    private const string InvalidRequest = "The request was not valid.";

    public static ApiError FromFailures(IReadOnlyCollection<ValidationFailure> failures) =>
        new(DomainCodeFor(failures), InvalidRequest, FieldsFor(failures));

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldsFor(
        IEnumerable<ValidationFailure> failures)
    {
        var fields = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(failure => failure.ErrorMessage)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        return fields.Count > 0 ? fields : null;
    }

    private static string? DomainCodeFor(IEnumerable<ValidationFailure> failures)
    {
        var distinctCodes = failures
            .Select(failure => failure.ErrorCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctCodes.Length != 1)
        {
            return null;
        }

        var sharedCode = distinctCodes[0];

        return ErrorCodes.All.Contains(sharedCode) ? sharedCode : null;
    }
}
