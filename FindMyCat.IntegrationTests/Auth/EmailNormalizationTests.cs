using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class EmailNormalizationTests : IntegrationTestBase
{
    public EmailNormalizationTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task Register_MixedCaseEmail_IsStoredNormalized()
    {
        await SeedAllowedEmailAsync("mixedcase@example.com");

        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("  MixedCase@Example.com  ", "Mixed Case", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        session!.Email.ShouldBe("mixedcase@example.com");
    }

    [Fact]
    public async Task Register_ThenLogin_WithDifferentCasing_Succeeds()
    {
        await SeedAllowedEmailAsync("sean@gmail.com");

        var registerResponse = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Sean@Gmail.com", "Sean", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var loginClient = CreateClient();
        var response = await loginClient.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("sean@gmail.com", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_SameMailboxDifferentCasing_ReturnsConflictInsteadOfCreatingASecondAccount()
    {
        await SeedAllowedEmailAsync("dup@example.com");

        var firstResponse = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Dup@Example.com", "First", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var secondClient = CreateClient();
        var response = await secondClient.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("dup@example.com", "Second", "An0ther!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Db.Users.CountAsync(u => u.Email == "dup@example.com", TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    private async Task SeedAllowedEmailAsync(string email)
    {
        Db.AllowedEmails.Add(new AllowedEmail
        {
            Id = Guid.NewGuid(),
            Email = email,
            AddedByUserId = Guid.NewGuid(),
            AddedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RemovingAllowedEmail_DeletesTheUserAccount_EvenWhenRequestCasingDiffers()
    {
        var admin = await CreateUserAsync(UserRole.Administrator, cancellationToken: TestContext.Current.CancellationToken);
        using var adminClient = CreateAuthenticatedClient(admin);

        var member = await CreateUserAsync(
            UserRole.User, email: "mixedcasemember@example.com", cancellationToken: TestContext.Current.CancellationToken);
        await adminClient.PostAsJsonAsync(
            "/api/admin/allowed-emails",
            new AddAllowedEmailRequest("mixedcasemember@example.com"),
            TestContext.Current.CancellationToken);

        var deleteResponse = await adminClient.DeleteAsync(
            "/api/admin/allowed-emails/MixedCaseMember@Example.com", TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == member.Id, TestContext.Current.CancellationToken))
            .ShouldBeNull();
    }
}
