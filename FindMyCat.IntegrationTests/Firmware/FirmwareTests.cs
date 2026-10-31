using System.Net;
using System.Net.Http.Headers;
using FindMyCat.IntegrationTests.Infrastructure;

namespace FindMyCat.IntegrationTests.Firmware;

public sealed class FirmwareTests : IntegrationTestBase
{
    private static readonly byte[] Image = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

    public FirmwareTests(FindMyCatApiFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
        File.WriteAllBytes(Path.Combine(Factory.FirmwareDirectory, "app_update.bin"), Image);
    }

    [Theory]
    [InlineData("/api/firmware/app_update.bin")]
    [InlineData("/api/device-commands/firmware/app_update.bin")]
    public async Task Anyone_can_download_an_image(string path)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(Image);
    }

    [Fact]
    public async Task A_download_can_resume_from_a_byte_offset()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/firmware/app_update.bin");
        request.Headers.Range = new RangeHeaderValue(90, null);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(Image[90..]);
    }

    [Theory]
    [InlineData("/api/firmware/missing.bin")]
    [InlineData("/api/firmware/..%2Fsecret")]
    [InlineData("/api/firmware/.hidden")]
    public async Task Unknown_or_unsafe_names_are_not_found(string path)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}