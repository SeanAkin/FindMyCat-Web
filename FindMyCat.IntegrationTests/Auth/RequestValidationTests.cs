using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Api.Errors;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Security;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Auth;

/// <summary>
/// Registration reports problems per field, so a form with three bad fields gets three sets of
/// messages back in one round trip instead of one message at a time.
/// </summary>
public sealed class RequestValidationTests : IntegrationTestBase
{
    private const string ValidEmail = "person@example.com";
    private const string ValidDisplayName = "Person";
    private const string ValidPassword = "Str0ng!Pass";

    public RequestValidationTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Theory]
    [InlineData("not-an-email", ValidDisplayName, ValidPassword, "email")]
    [InlineData("", ValidDisplayName, ValidPassword, "email")]
    [InlineData(ValidEmail, "", ValidPassword, "displayName")]
    [InlineData(ValidEmail, ValidDisplayName, "", "password")]
    [InlineData("not-an-email", "", ValidPassword, "email", "displayName")]
    [InlineData("not-an-email", "", "", "email", "displayName", "password")]
    public async Task Register_WithInvalidFields_ReportsEveryOffendingFieldAtOnce(
        string email, string displayName, string password, params string[] expectedFields)
    {
        var body = await PostRegistrationAsync(new RegisterRequest(email, displayName, password));

        body.Code.ShouldBeNull();
        body.Errors.ShouldNotBeNull();
        body.Errors.Keys.OrderBy(field => field).ShouldBe(expectedFields.OrderBy(field => field));
        body.Errors.Values.ShouldAllBe(messages => messages.Count > 0);
    }

    [Theory]
    [InlineData("email", 321)]
    [InlineData("displayName", 256)]
    [InlineData("password", PasswordPolicy.MaximumLength + 1)]
    public async Task Register_WithOverlongField_RejectsThatFieldAlone(string field, int length)
    {
        var request = field switch
        {
            "email" => new RegisterRequest(EmailOfLength(length), ValidDisplayName, ValidPassword),
            "displayName" => new RegisterRequest(ValidEmail, new string('n', length), ValidPassword),
            "password" => new RegisterRequest(ValidEmail, ValidDisplayName, PasswordOfLength(length)),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unhandled field.")
        };

        var body = await PostRegistrationAsync(request);

        body.Errors.ShouldNotBeNull();
        body.Errors.Keys.ShouldBe([field]);
    }

    /// <summary>
    /// The policy returns each unmet requirement separately; they must survive the trip to the
    /// client as separate messages rather than being flattened into one sentence.
    /// </summary>
    [Theory]
    [InlineData("weak", new[] { "at least 8 characters", "uppercase letter", "one symbol" })]
    [InlineData("weakpassword", new[] { "uppercase letter", "one symbol" })]
    [InlineData("Weakpassword", new[] { "one symbol" })]
    [InlineData("weakpassword!", new[] { "uppercase letter" })]
    public async Task Register_WithWeakPassword_ReportsEveryUnmetRequirementSeparately(
        string password, string[] expectedRequirements)
    {
        var body = await PostRegistrationAsync(new RegisterRequest(ValidEmail, ValidDisplayName, password));

        body.Code.ShouldBe(ErrorCodes.WeakPassword);
        body.Errors.ShouldNotBeNull();
        body.Errors.Keys.ShouldBe([WeakPasswordException.PasswordField]);

        var messages = body.Errors[WeakPasswordException.PasswordField];
        messages.Count.ShouldBe(expectedRequirements.Length);

        foreach (var requirement in expectedRequirements)
        {
            messages.ShouldContain(
                message => message.Contains(requirement, StringComparison.OrdinalIgnoreCase),
                $"No message mentioned '{requirement}'.");
        }
    }

    [Fact]
    public async Task Register_WithValidRequest_ReportsNoFieldErrors()
    {
        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest($"{Guid.NewGuid():N}@example.com", ValidDisplayName, ValidPassword),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
    }

    private async Task<ApiError> PostRegistrationAsync(RegisterRequest request)
    {
        var response = await Client.PostAsJsonAsync(
            "/auth/register", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ApiError>(
            JsonOptions, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Message.ShouldNotBeNullOrWhiteSpace();
        return body;
    }

    private static string EmailOfLength(int length)
    {
        const string domain = "@example.com";
        return new string('a', length - domain.Length) + domain;
    }

    private static string PasswordOfLength(int length)
    {
        const string strongPrefix = "Str0ng!";
        return strongPrefix + new string('a', length - strongPrefix.Length);
    }
}
