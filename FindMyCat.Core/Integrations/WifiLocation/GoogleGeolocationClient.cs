using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FindMyCat.Core.Integrations.WifiLocation;

public sealed record GeolocationSettings(string? ApiKey);

public sealed record GeoFix(double Latitude, double Longitude, double AccuracyMeters);

public interface IGoogleGeolocationClient
{
    Task<GeoFix> LocateAsync(IReadOnlyList<WifiAccessPoint> accessPoints, CancellationToken cancellationToken = default);
}

public sealed class GoogleGeolocationClient(HttpClient httpClient, GeolocationSettings settings) : IGoogleGeolocationClient
{
    public async Task<GeoFix> LocateAsync(IReadOnlyList<WifiAccessPoint> accessPoints, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new WifiLocateException("no google geolocation key");
        }

        var request = new { considerIp = false, wifiAccessPoints = accessPoints };
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"geolocation/v1/geolocate?key={Uri.EscapeDataString(settings.ApiKey)}", request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new WifiLocateException("wifi not known to google");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GeolocateResponse>(cancellationToken)
                         ?? throw new WifiLocateException("location service unreachable");
            return new GeoFix(result.Location.Lat, result.Location.Lng, result.Accuracy);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new WifiLocateException("location service unreachable");
        }
    }

    private sealed record GeolocateResponse(GeolocateLocation Location, double Accuracy);

    private sealed record GeolocateLocation(double Lat, double Lng);
}