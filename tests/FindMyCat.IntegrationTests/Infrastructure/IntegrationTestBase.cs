using System.Text.Json;
using System.Text.Json.Serialization;
using FindMyCat.Core.Entities;
using FindMyCat.Data;
using FindMyCat.IntegrationTests.Stubs;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyCat.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<FindMyCatApiFactory>, IDisposable
{
    private static readonly AsyncLocal<ITestOutputHelper?> OutputHolder = new();

    internal static ITestOutputHelper? CurrentOutput => OutputHolder.Value;

    protected static readonly JsonSerializerOptions JsonOptionsMatchingServerEnumSerialization = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected readonly FindMyCatApiFactory Factory;
    protected readonly HttpClient Client;

    private readonly IServiceScope _scope;
    private AppDbContext? _db;

    protected AppDbContext Db => _db ??= _scope.ServiceProvider.GetRequiredService<AppDbContext>();

    protected StubTraccarClient Traccar => Factory.TraccarClient;
    protected StubHologramClient Hologram => Factory.HologramClient;

    protected IntegrationTestBase(FindMyCatApiFactory factory, ITestOutputHelper output)
    {
        OutputHolder.Value = output;
        Factory = factory;
        Client = CreateClient();
        _scope = factory.Services.CreateScope();
        factory.TraccarClient.Reset();
        factory.HologramClient.Reset();
        ResetSharedCredentialVault();
    }

    private void ResetSharedCredentialVault()
    {
        Db.SharedCredentials.RemoveRange(Db.SharedCredentials);
        Db.SaveChanges();
    }

    protected HttpClient CreateClient() => Factory.CreateDefaultClient(new TestResponseLoggingHandler());

    protected HttpClient CreateAuthenticatedClient(Guid userId, string email, string displayName, UserRole role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthDefaults.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthDefaults.EmailHeader, email);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.NameHeader, displayName);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.RoleHeader, role.ToString());
        return client;
    }

    protected HttpClient CreateAuthenticatedClient(User user) =>
        CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName, user.Role);
    
    protected async Task<User> CreateUserAsync(
        UserRole role = UserRole.User,
        string? email = null,
        string? displayName = null,
        bool isPrimaryAdministrator = false,
        CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleSubjectId = Guid.NewGuid().ToString(),
            Email = email ?? $"{Guid.NewGuid():N}@example.com",
            DisplayName = displayName ?? "Test User",
            Role = role,
            IsPrimaryAdministrator = isPrimaryAdministrator,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };

        Db.Users.Add(user);
        await Db.SaveChangesAsync(cancellationToken);

        return user;
    }

    public void Dispose()
    {
        Client.Dispose();
        _scope.Dispose();
        OutputHolder.Value = null;
        GC.SuppressFinalize(this);
    }
}
