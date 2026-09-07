using FindMyCat.Api.Contracts;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Security;
using FluentValidation;
using FluentValidation.Results;

namespace FindMyCat.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Password).Custom(ReportPasswordPolicyViolations);
    }

    private static void ReportPasswordPolicyViolations(string? password, ValidationContext<RegisterRequest> context)
    {
        foreach (var violation in PasswordPolicy.GetViolations(password ?? string.Empty))
        {
            context.AddFailure(new ValidationFailure(context.PropertyPath, violation)
            {
                ErrorCode = ErrorCodes.WeakPassword
            });
        }
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleForPasswordShapeOnly();
    }

    private void RuleForPasswordShapeOnly() =>
        RuleFor(request => request.Password).NotEmpty().MaximumLength(PasswordPolicy.MaximumLength);
}
