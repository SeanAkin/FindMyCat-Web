using FindMyCat.Api.Contracts;
using FluentValidation;

namespace FindMyCat.Api.Validation;

public sealed class AddAllowedEmailRequestValidator : AbstractValidator<AddAllowedEmailRequest>
{
    public AddAllowedEmailRequestValidator() =>
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
}

public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator() =>
        RuleFor(request => request.Role).IsInEnum();
}
