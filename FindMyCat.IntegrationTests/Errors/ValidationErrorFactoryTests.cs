using FindMyCat.Api.Errors;
using FindMyCat.Core.Errors;
using FluentValidation.Results;

namespace FindMyCat.IntegrationTests.Errors;

public sealed class ValidationErrorFactoryTests
{
    [Fact]
    public void Every_failure_on_one_field_is_reported_separately()
    {
        var error = ValidationErrorFactory.FromFailures([
            Failure("password", "Too short."),
            Failure("password", "Needs a symbol.")
        ]);

        error.Errors.ShouldNotBeNull();
        error.Errors["password"].ShouldBe(["Too short.", "Needs a symbol."]);
    }

    [Fact]
    public void Failures_on_different_fields_are_kept_apart()
    {
        var error = ValidationErrorFactory.FromFailures([
            Failure("email", "Not an email address."),
            Failure("displayName", "Required.")
        ]);

        error.Errors.ShouldNotBeNull();
        error.Errors.Keys.OrderBy(field => field).ShouldBe(["displayName", "email"]);
    }

    [Fact]
    public void One_field_rejected_twice_for_the_same_reason_says_so_once()
    {
        var error = ValidationErrorFactory.FromFailures([
            Failure("email", "Required."),
            Failure("email", "Required.")
        ]);

        error.Errors.ShouldNotBeNull();
        error.Errors["email"].ShouldBe(["Required."]);
    }

    [Fact]
    public void A_domain_code_shared_by_every_failure_is_promoted_onto_the_response()
    {
        var error = ValidationErrorFactory.FromFailures([
            Failure("password", "Too short.", ErrorCodes.WeakPassword),
            Failure("password", "Needs a symbol.", ErrorCodes.WeakPassword)
        ]);

        error.Code.ShouldBe(ErrorCodes.WeakPassword);
    }

    [Fact]
    public void A_domain_code_is_withheld_when_other_failures_do_not_share_it()
    {
        var error = ValidationErrorFactory.FromFailures([
            Failure("email", "Not an email address."),
            Failure("password", "Too short.", ErrorCodes.WeakPassword)
        ]);

        error.Code.ShouldBeNull();
    }

    [Fact]
    public void The_error_codes_fluent_validation_assigns_its_own_rules_are_not_promoted()
    {
        var error = ValidationErrorFactory.FromFailures([Failure("email", "Required.", "NotEmptyValidator")]);

        error.Code.ShouldBeNull();
    }

    private static ValidationFailure Failure(string property, string message, string? errorCode = null) =>
        new(property, message) { ErrorCode = errorCode };
}
