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

public sealed class DeviceEventsRequestValidator : AbstractValidator<DeviceEventsRequest>
{
    public DeviceEventsRequestValidator()
    {
        RuleFor(request => request.After).GreaterThanOrEqualTo(0).When(request => request.After.HasValue);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100).When(request => request.Limit.HasValue);
    }
}
