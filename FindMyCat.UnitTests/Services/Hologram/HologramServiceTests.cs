using FindMyCat.Core.Services;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using Moq;

namespace FindMyCat.UnitTests.Services.Hologram;

public class HologramServiceTests
{
    private readonly Mock<ICredentialService> _credentials = new();
    private readonly Mock<ITraccarService> _traccar = new();
    private readonly Mock<IHologramClient> _client = new();
    private readonly HologramService _sut;

    public HologramServiceTests()
    {
        _sut = new HologramService(_credentials.Object, _traccar.Object, _client.Object);
    }

    private void HasApiKey(string? apiKey) =>
        _credentials.Setup(c => c.GetHologramKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(apiKey);

    private void HasTraccarDevices(params TraccarDevice[] devices) =>
        _traccar.Setup(t => t.GetDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(devices);

    [Fact]
    public async Task SendCommand_throws_not_configured_when_no_api_key()
    {
        HasApiKey(null);

        await Should.ThrowAsync<HologramNotConfiguredException>(
            () => _sut.SendCommandAsync(1, HologramCommand.Ping));

        _traccar.Verify(t => t.GetDevicesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendCommand_throws_device_not_found_when_traccar_device_missing()
    {
        HasApiKey("key");
        HasTraccarDevices();

        await Should.ThrowAsync<HologramDeviceNotFoundException>(
            () => _sut.SendCommandAsync(1, HologramCommand.Ping));

        _client.Verify(c => c.FindDeviceIdByImeiAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendCommand_throws_device_not_found_when_hologram_has_no_matching_device()
    {
        HasApiKey("key");
        HasTraccarDevices(new TraccarDevice(1, "Nova", "unique-1", "online", null, false, null));
        _client.Setup(c => c.FindDeviceIdByImeiAsync("key", "unique-1", It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        await Should.ThrowAsync<HologramDeviceNotFoundException>(
            () => _sut.SendCommandAsync(1, HologramCommand.Lost));

        _client.Verify(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(HologramCommand.Ping, "ping")]
    [InlineData(HologramCommand.Lost, "lost")]
    [InlineData(HologramCommand.Active, "active")]
    public async Task SendCommand_resolves_device_and_sends_wire_command(HologramCommand command, string wireValue)
    {
        HasApiKey("key");
        HasTraccarDevices(new TraccarDevice(1, "Nova", "unique-1", "online", null, false, null));
        _client.Setup(c => c.FindDeviceIdByImeiAsync("key", "unique-1", It.IsAny<CancellationToken>())).ReturnsAsync(42);

        await _sut.SendCommandAsync(1, command, TestContext.Current.CancellationToken);

        _client.Verify(c => c.SendMessageAsync("key", 42, wireValue, It.IsAny<CancellationToken>()), Times.Once);
    }
}
