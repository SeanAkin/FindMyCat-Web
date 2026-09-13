using FindMyCat.Api.Contracts;
using FluentValidation;

namespace FindMyCat.Api.Validation;

public sealed class HistoryRangeRequestValidator : AbstractValidator<HistoryRangeRequest>
{
    public HistoryRangeRequestValidator()
    {
        RuleFor(request => request.From).NotNull();
        RuleFor(request => request.To).NotNull();
    }
}
