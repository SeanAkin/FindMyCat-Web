using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FindMyCat.IntegrationTests.Auth;

public sealed class PasswordAuthenticationTests : IClassFixture<PasswordAuthenticationTests.PasswordAuthFactory>
{
    private readonly PasswordAuthFactory _factory;

    public PasswordAuthenticationTests(PasswordAuthFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_FirstUser_CreatesAdministratorAndSignsInViaCookie()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        session!.Email.ShouldBe("founder@example.com");
        session.Role.ShouldBe("Administrator");

        var sessionResponse = await client.GetAsync("/auth/session", TestContext.Current.CancellationToken);
        sessionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var storedUser = await scope.Db.Users.SingleAsync(u => u.Email == "founder@example.com", TestContext.Current.CancellationToken);
        storedUser.IsPrimaryAdministrator.ShouldBeTrue();
        storedUser.GoogleSubjectId.ShouldBeNull();
        storedUser.PasswordHash.ShouldNotBeNullOrWhiteSpace();
        storedUser.PasswordHash.ShouldNotBe("Str0ng!Pass");
    }

    [Fact]
    public async Task Register_NotAllowListed_ReturnsForbiddenWithoutCreatingUser()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        await scope.SeedPrimaryAdministratorAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("stranger@example.com", "Stranger", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("not_allow_listed");
        (await scope.Db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        await using var scope = await _factory.FreshDatabaseAsync();

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "weak"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("weak_password");
    }

    [Fact]
    public async Task Register_AllowListedEmail_CreatesStandardUser()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        await scope.SeedPrimaryAdministratorAsync();
        await scope.SeedAllowedEmailAsync("friend@example.com");
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("friend@example.com", "Friend", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        session!.Role.ShouldBe("User");
    }

    [Fact]
    public async Task Register_EmailAlreadyRegistered_ReturnsConflict()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        using var otherClient = _factory.CreateClient();
        var response = await otherClient.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Someone Else", "An0ther!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("email_already_registered");
        (await scope.Db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Login_CorrectPassword_SignsInViaCookie()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        using var registerClient = _factory.CreateClient();
        await registerClient.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("founder@example.com", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessionResponse = await client.GetAsync("/auth/session", TestContext.Current.CancellationToken);
        sessionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        using var registerClient = _factory.CreateClient();
        await registerClient.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("founder@example.com", "Wrong!Pass1"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("nobody@example.com", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_EmailAlreadyLinkedToGoogleAccount_ReturnsConflict()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        await scope.SeedGoogleUserAsync("founder@example.com");
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("email_already_registered");
    }

    [Fact]
    public async Task DeletedUser_LosesSessionOnNextRequest()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("founder@example.com", "Founder", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        var beforeDelete = await client.GetAsync("/auth/session", TestContext.Current.CancellationToken);
        beforeDelete.StatusCode.ShouldBe(HttpStatusCode.OK);

        await scope.Db.Users
            .Where(u => u.Email == "founder@example.com")
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        var afterDelete = await client.GetAsync("/auth/session", TestContext.Current.CancellationToken);
        afterDelete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PromotedUser_GainsAdminAccessOnNextRequest_WithoutReLogin()
    {
        await using var scope = await _factory.FreshDatabaseAsync();
        await scope.SeedPrimaryAdministratorAsync();
        await scope.SeedAllowedEmailAsync("member@example.com");

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("member@example.com", "Member", "Str0ng!Pass"),
            TestContext.Current.CancellationToken);

        var beforePromotion = await client.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);
        beforePromotion.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await scope.Db.Users
            .Where(u => u.Email == "member@example.com")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(u => u.Role, UserRole.Administrator),
                TestContext.Current.CancellationToken);

        var afterPromotion = await client.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);
        afterPromotion.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public sealed class PasswordAuthFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"findmycat-pwdauth-{Guid.NewGuid():N}.db");

        private string ConnectionString => $"Data Source={_dbPath}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FINDMYCAT_ENCRYPTION_KEY"] = "gFpnIeo9r9VhDpFCizlLsBa/0ARQH/nMsdfnz1lCQeI=",
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Authentication:Google:ClientId"] = "test-client-id",
                ["Authentication:Google:ClientSecret"] = "test-client-secret",
                ["Traccar:BaseUrl"] = "https://traccar.invalid/"
            }));

            return base.CreateHost(builder);
        }

        public async ValueTask InitializeAsync()
        {
            await using var db = CreateDbContext();
            await db.Database.MigrateAsync();
        }

        public async Task<DatabaseScope> FreshDatabaseAsync()
        {
            var db = CreateDbContext();
            db.Users.RemoveRange(db.Users);
            db.AllowedEmails.RemoveRange(db.AllowedEmails);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            return new DatabaseScope(db);
        }

        private AppDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite(ConnectionString);
            return new AppDbContext(optionsBuilder.Options);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();

            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var path = _dbPath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    public sealed class DatabaseScope(AppDbContext db) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;

        public async Task SeedAllowedEmailAsync(string email)
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

        public async Task SeedPrimaryAdministratorAsync()
        {
            Db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = "existing-admin@example.com",
                DisplayName = "Existing Admin",
                GoogleSubjectId = "google-existing-admin",
                Role = UserRole.Administrator,
                IsPrimaryAdministrator = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SeedGoogleUserAsync(string email)
        {
            Db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = "Founder",
                GoogleSubjectId = "google-founder",
                Role = UserRole.Administrator,
                IsPrimaryAdministrator = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
