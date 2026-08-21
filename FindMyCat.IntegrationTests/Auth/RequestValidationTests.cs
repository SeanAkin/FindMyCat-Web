using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Services;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class RequestValidationTests : IntegrationTestBase
{
    public RequestValidationTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Register_PasswordLongerThanMaximum_ReturnsBadRequest()
    {
        var tooLongPassword = "Str0ng!" + new string('a', PasswordPolicy.MaximumLength);

        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("toolongpassword@example.com", "Person", tooLongPassword),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_PasswordLongerThanMaximum_ReturnsBadRequest()
    {
        var tooLongPassword = "Str0ng!" + new string('a', PasswordPolicy.MaximumLength);

        var response = await Client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("someone@example.com", tooLongPassword),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_EmailLongerThanMaximum_ReturnsBadRequest()
    {
        var tooLongEmail = new string('a', 315) + "@example.com";

        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest(tooLongEmail, "Person", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
