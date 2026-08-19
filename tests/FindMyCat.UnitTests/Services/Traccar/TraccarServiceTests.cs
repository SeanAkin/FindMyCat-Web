using FindMyCat.Core.Services;
using FindMyCat.Core.Services.Traccar;
using Moq;

namespace FindMyCat.UnitTests.Services.Traccar;

public class TraccarServiceTests
{
    private readonly Mock<ICredentialService> _credentials = new();
    private readonly Mock<ITraccarClient> _client = new();
    private readonly TraccarService _sut;

    public TraccarServiceTests()
    {
        _sut = new TraccarService(_credentials.Object, _client.Object);
    }

    private void HasToken(string? token) =>
        _credentials.Setup(c => c.GetTraccarTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

    [Fact]
    public async Task GetDevices_throws_not_configured_when_no_token()
    {
        HasToken(null);

        await Should.ThrowAsync<TraccarNotConfiguredException>(() => _sut.GetDevicesAsync());
        _client.Verify(c => c.GetDevicesWithPositionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDevices_passes_stored_token_to_client()
    {
        HasToken("stored-token");
        _client.Setup(c => c.GetDevicesWithPositionsAsync("stored-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.GetDevicesAsync();

        _client.Verify(c => c.GetDevicesWithPositionsAsync("stored-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestPosition_returns_first_position()
    {
        HasToken("tok");
        var position = new TraccarPosition(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            1, 2, 0, 0, 0, 5, true, 42, 9);
        _client.Setup(c => c.GetPositionsAsync("tok", 1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([position]);

        var result = await _sut.GetLatestPositionAsync(1);

        result.ShouldBe(position);
    }

    [Fact]
    public async Task GetLatestPosition_returns_null_when_device_has_no_position()
    {
        HasToken("tok");
        _client.Setup(c => c.GetPositionsAsync("tok", 1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetLatestPositionAsync(1);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetHistory_forwards_range_to_client()
    {
        HasToken("tok");
        var from = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2025, 1, 3, 0, 0, 0, TimeSpan.Zero);
        _client.Setup(c => c.GetPositionsAsync("tok", 1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.GetHistoryAsync(1, from, to);

        _client.Verify(c => c.GetPositionsAsync("tok", 1, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }
}
