using FindMyCat.Api.Contracts;
using FluentValidation;

namespace FindMyCat.Api.Validation;

public sealed class SetTraccarCredentialRequestValidator : AbstractValidator<SetTraccarCredentialRequest>
{
    public SetTraccarCredentialRequestValidator() =>
        RuleFor(request => request.ApiToken).NotEmpty();
}

public sealed class SetHologramCredentialRequestValidator : AbstractValidator<SetHologramCredentialRequest>
{
    public SetHologramCredentialRequestValidator() =>
        RuleFor(request => request.ApiKey).NotEmpty();
}
