using System.Net;
using System.Net.Http.Json;
using FindMyCat.Api.Contracts;
using FindMyCat.Api.Errors;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class DeviceManagementTests : IntegrationTestBase
{
    private static readonly SaveDeviceRequest Collar = new("Mochi", "collar-1", new DeviceAttributes("🐈"));

    public DeviceManagementTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    private async Task<HttpClient> CreateClientAsync(UserRole role)
    {
        var user = await CreateUserAsync(role, cancellationToken: TestContext.Current.CancellationToken);
        await ConfigureTraccarTokenAsync();
        return CreateAuthenticatedClient(user);
    }

    [Fact]
    public async Task Admin_creates_a_device()
    {
        using var client = await CreateClientAsync(UserRole.Administrator);

        var response = await client.PostAsJsonAsync("/api/devices", Collar, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>(TestContext.Current.CancellationToken);
        device!.Name.ShouldBe("Mochi");
        device.UniqueId.ShouldBe("collar-1");
        device.Attributes.Emoji.ShouldBe("🐈");
        Traccar.Devices.ShouldContain(d => d.UniqueId == "collar-1");
    }

    [Fact]
    public async Task Admin_updates_a_device()
    {
        using var client = await CreateClientAsync(UserRole.Administrator);
        Traccar.Devices = [new TraccarDevice(1, "Old", "collar-1", "online", null, false, null, "🐕")];

        var response = await client.PutAsJsonAsync("/api/devices/1", Collar, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>(TestContext.Current.CancellationToken);
        device!.Name.ShouldBe("Mochi");
        device.Attributes.Emoji.ShouldBe("🐈");
    }

    [Fact]
    public async Task Admin_deletes_a_device()
    {
        using var client = await CreateClientAsync(UserRole.Administrator);
        Traccar.Devices = [new TraccarDevice(1, "Mochi", "collar-1", "online", null, false, null)];

        var response = await client.DeleteAsync("/api/devices/1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Traccar.Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_returns_404_for_an_unknown_device()
    {
        using var client = await CreateClientAsync(UserRole.Administrator);

        var response = await client.PutAsJsonAsync("/api/devices/99", Collar, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiError>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe(ErrorCodes.DeviceNotFound);
    }

    [Fact]
    public async Task Create_returns_400_when_traccar_rejects_the_device()
    {
        using var client = await CreateClientAsync(UserRole.Administrator);
        Traccar.ThrowInstead = new TraccarDeviceRejectedException("duplicate");

        var response = await client.PostAsJsonAsync("/api/devices", Collar, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiError>(TestContext.Current.CancellationToken);
        body!.Code.ShouldBe(ErrorCodes.DeviceRejected);
    }

    [Fact]
    public async Task Create_rejects_a_missing_emoji()
    {
        using var client = await CreateClientAsync(UserRole.Administrator);

        var response = await client.PostAsJsonAsync(
            "/api/devices", Collar with { Attributes = new DeviceAttributes("") }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Traccar.Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Non_admin_cannot_create_update_or_delete()
    {
        using var client = await CreateClientAsync(UserRole.User);
        Traccar.Devices = [new TraccarDevice(1, "Mochi", "collar-1", "online", null, false, null)];

        var create = await client.PostAsJsonAsync("/api/devices", Collar, TestContext.Current.CancellationToken);
        var update = await client.PutAsJsonAsync("/api/devices/1", Collar, TestContext.Current.CancellationToken);
        var delete = await client.DeleteAsync("/api/devices/1", TestContext.Current.CancellationToken);

        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        update.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Traccar.Devices.Count.ShouldBe(1);
    }
}