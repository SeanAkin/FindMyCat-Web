using System.Text.Json;
using FindMyCat.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace FindMyCat.IntegrationTests.Errors;

public sealed class ValidationErrorFactoryTests
{
    [Theory]
    [InlineData("Email", "email")]
    [InlineData("$.email", "email")]
    [InlineData("DisplayName", "displayName")]
    [InlineData("Cat.Name", "cat.name")]
    [InlineData("$.cat.name", "cat.name")]
    [InlineData("Cats[0].Name", "cats[0].name")]
    [InlineData("$[0].Name", "[0].name")]
    public void Field_keys_are_normalised_to_the_naming_the_payload_uses(string modelStateKey, string expectedField)
    {
        var error = Build(JsonNamingPolicy.CamelCase, (modelStateKey, "Required."));

        error.Errors.ShouldNotBeNull();
        error.Errors.Keys.ShouldBe([expectedField]);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("$")]
    public void Problems_belonging_to_no_field_are_kept_under_the_request_key(string modelStateKey)
    {
        var error = Build(JsonNamingPolicy.CamelCase, (modelStateKey, "The body could not be read."));

        error.Errors.ShouldNotBeNull();
        error.Errors.Keys.ShouldBe([ValidationErrorFactory.RequestField]);
        error.Errors[ValidationErrorFactory.RequestField].ShouldBe(["The body could not be read."]);
    }

    [Theory]
    [InlineData("snake", "display_name")]
    [InlineData("none", "DisplayName")]
    public void Field_keys_follow_the_configured_naming_policy(string policyName, string expectedField)
    {
        var policy = policyName == "snake" ? JsonNamingPolicy.SnakeCaseLower : null;

        var error = Build(policy, ("DisplayName", "Required."));

        error.Errors.ShouldNotBeNull();
        error.Errors.Keys.ShouldBe([expectedField]);
    }

    [Fact]
    public void Two_keys_naming_the_same_field_do_not_repeat_an_identical_message()
    {
        var error = Build(JsonNamingPolicy.CamelCase, ("Email", "Required."), ("$.email", "Required."));

        error.Errors.ShouldNotBeNull();
        error.Errors.Keys.ShouldBe(["email"]);
        error.Errors["email"].ShouldBe(["Required."]);
    }

    [Fact]
    public void Different_messages_for_one_field_are_all_kept()
    {
        var error = Build(
            JsonNamingPolicy.CamelCase, ("Email", "Required."), ("Email", "Not an email address."));

        error.Errors.ShouldNotBeNull();
        error.Errors["email"].Count.ShouldBe(2);
    }

    [Fact]
    public void An_entry_carrying_only_an_exception_yields_a_message_without_the_exception_text()
    {
        const string internalDetail = "0x80004005 at JsonReader.Read";
        var modelState = new ModelStateDictionary();
        modelState.TryAddModelException("$.password", new JsonException(internalDetail));

        var error = Factory(JsonNamingPolicy.CamelCase).FromModelState(modelState);

        error.Errors.ShouldNotBeNull();
        error.Errors["password"].ShouldNotBeEmpty();
        error.Errors["password"].ShouldAllBe(message => !message.Contains(internalDetail));
    }

    [Fact]
    public void A_request_with_no_problems_carries_no_errors_member()
    {
        var error = Factory(JsonNamingPolicy.CamelCase).FromModelState(new ModelStateDictionary());

        error.Code.ShouldBeNull();
        error.Message.ShouldNotBeNullOrWhiteSpace();
        error.Errors.ShouldBeNull();
    }

    private static ApiError Build(JsonNamingPolicy? policy, params (string Key, string Message)[] errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (key, message) in errors)
        {
            modelState.AddModelError(key, message);
        }

        return Factory(policy).FromModelState(modelState);
    }

    private static ValidationErrorFactory Factory(JsonNamingPolicy? policy)
    {
        var options = new JsonOptions();
        options.JsonSerializerOptions.PropertyNamingPolicy = policy;
        return new ValidationErrorFactory(Options.Create(options));
    }
}
