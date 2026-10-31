using System.Net;
using System.Text;
using System.Text.Json;
using FindMyCat.Core.Integrations.WifiLocation;
using FindMyCat.UnitTests.Infrastructure;

namespace FindMyCat.UnitTests.Services.WifiLocation;

public sealed class GoogleGeolocationClientTests
{
    private static readonly WifiAccessPoint[] AccessPoints =
    [
        new("aa:bb:cc:00:11:22", -48),
        new("01:02:03:04:05:06", -71)
    ];

    private static GoogleGeolocationClient CreateClient(StubHttpMessageHandler handler, string? apiKey = "google-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://google.test/") }, new GeolocationSettings(apiKey));

    [Fact]
    public async Task Sends_the_access_points_and_returns_the_fix()
    {
        string? sentBody = null;
        Uri? sentUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            sentUri = request.RequestUri;
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"location":{"lat":47.61,"lng":-122.33},"accuracy":24.5}""", Encoding.UTF8, "application/json")
            };
        });

        var fix = await CreateClient(handler).LocateAsync(AccessPoints, TestContext.Current.CancellationToken);

        fix.ShouldBe(new GeoFix(47.61, -122.33, 24.5));
        sentUri!.AbsolutePath.ShouldBe("/geolocation/v1/geolocate");
        sentUri.Query.ShouldBe("?key=google-key");
        var body = JsonDocument.Parse(sentBody!).RootElement;
        body.GetProperty("considerIp").GetBoolean().ShouldBeFalse();
        var first = body.GetProperty("wifiAccessPoints")[0];
        first.GetProperty("macAddress").GetString().ShouldBe("aa:bb:cc:00:11:22");
        first.GetProperty("signalStrength").GetInt32().ShouldBe(-48);
    }

    [Fact]
    public async Task Unknown_wifi_gives_a_short_reason()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.NotFound, """{"error":{}}"""));

        var ex = await Should.ThrowAsync<WifiLocateException>(() => CreateClient(handler).LocateAsync(AccessPoints));

        ex.Message.ShouldBe("wifi not known to google");
    }

    [Fact]
    public async Task A_server_error_means_the_service_is_unreachable()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.InternalServerError, "{}"));

        var ex = await Should.ThrowAsync<WifiLocateException>(() => CreateClient(handler).LocateAsync(AccessPoints));

        ex.Message.ShouldBe("location service unreachable");
    }

    [Fact]
    public async Task Without_a_key_nothing_is_sent()
    {
        var handler = StubHttpMessageHandler.ReturningJson(_ => (HttpStatusCode.OK, "{}"));

        var ex = await Should.ThrowAsync<WifiLocateException>(
            () => CreateClient(handler, apiKey: null).LocateAsync(AccessPoints));

        ex.Message.ShouldBe("no google geolocation key");
        handler.Requests.ShouldBeEmpty();
    }
}