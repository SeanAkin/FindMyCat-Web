using System.Text.Json;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FindMyCat.UnitTests.Services;

public class TelemetryIngestServiceTests
{
    private readonly Mock<IDeviceEventRepository> _repository = new();
    private readonly TelemetryIngestService _sut;

    public TelemetryIngestServiceTests()
    {
        _sut = new TelemetryIngestService(_repository.Object, NullLogger<TelemetryIngestService>.Instance);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task RecordStatusAsync_UsesExplicitKind()
    {
        DeviceEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DeviceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceEvent, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _sut.RecordStatusAsync("collar-1", Json("""{"kind":"radio","link":"LoRa"}"""),
            TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.UniqueId.ShouldBe("collar-1");
        saved.Kind.ShouldBe("radio");
        saved.Payload.ShouldContain("LoRa");
    }

    [Fact]
    public async Task RecordStatusAsync_MapsNetCommandToNetwork()
    {
        DeviceEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DeviceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceEvent, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _sut.RecordStatusAsync("collar-1", Json("""{"command":"NET","edrx_s":81.92}"""),
            TestContext.Current.CancellationToken);

        saved!.Kind.ShouldBe("network");
    }

    [Fact]
    public async Task RecordStatusAsync_DefaultsCommandedReportsToCommand()
    {
        DeviceEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DeviceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceEvent, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _sut.RecordStatusAsync("collar-1", Json("""{"command":"ping","status":"ok"}"""),
            TestContext.Current.CancellationToken);

        saved!.Kind.ShouldBe("command");
    }

    [Fact]
    public async Task RecordStatusAsync_ParsesPayloadProvidedAsJsonString()
    {
        DeviceEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DeviceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceEvent, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        var payloadAsString = JsonSerializer.SerializeToElement("""{"command":"ping","status":"ok"}""");

        await _sut.RecordStatusAsync("collar-1", payloadAsString, TestContext.Current.CancellationToken);

        saved!.Kind.ShouldBe("command");
        saved.Payload.ShouldContain("ping");
    }

    [Fact]
    public async Task RecordStatusAsync_Throws_WhenNeitherKindNorCommandPresent()
    {
        await Should.ThrowAsync<TelemetryPayloadException>(
            () => _sut.RecordStatusAsync("collar-1", Json("""{"battery":80}"""),
                TestContext.Current.CancellationToken));

        _repository.Verify(r => r.AddAsync(It.IsAny<DeviceEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordStatusAsync_Throws_WhenPayloadIsNotAnObject()
    {
        await Should.ThrowAsync<TelemetryPayloadException>(
            () => _sut.RecordStatusAsync("collar-1", Json("""[1,2,3]"""),
                TestContext.Current.CancellationToken));
    }
}
