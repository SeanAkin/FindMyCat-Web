using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FindMyCat.Core.Integrations.CellLocation;
using FindMyCat.Core.Integrations.WifiLocation;

namespace FindMyCat.Core.Integrations.Geolocation;

public sealed record GeolocationSettings(string? ApiKey);

public sealed record GeoFix(double Latitude, double Longitude, double AccuracyMeters);

public interface IGoogleGeolocationClient
{
    Task<GeoFix> LocateAsync(IReadOnlyList<WifiAccessPoint> accessPoints, CancellationToken cancellationToken = default);

    Task<GeoFix> LocateAsync(IReadOnlyList<CellTower> cellTowers, CancellationToken cancellationToken = default);
}

public sealed class GoogleGeolocationClient(HttpClient httpClient, GeolocationSettings settings) : IGoogleGeolocationClient
{
    public Task<GeoFix> LocateAsync(IReadOnlyList<WifiAccessPoint> accessPoints, CancellationToken cancellationToken = default) =>
        GeolocateAsync(new { considerIp = false, wifiAccessPoints = accessPoints }, "wifi not known to google", cancellationToken);

    // The collar only measures LTE cells.
    public Task<GeoFix> LocateAsync(IReadOnlyList<CellTower> cellTowers, CancellationToken cancellationToken = default) =>
        GeolocateAsync(new { considerIp = false, radioType = "lte", cellTowers }, "cells not known to google", cancellationToken);

    private async Task<GeoFix> GeolocateAsync(object request, string notFoundReason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new GeolocationException("no google geolocation key");
        }

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"geolocation/v1/geolocate?key={Uri.EscapeDataString(settings.ApiKey)}", request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new GeolocationException(notFoundReason);
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GeolocateResponse>(cancellationToken)
                         ?? throw new GeolocationException("location service unreachable");
            return new GeoFix(result.Location.Lat, result.Location.Lng, result.Accuracy);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new GeolocationException("location service unreachable");
        }
    }

    private sealed record GeolocateResponse(GeolocateLocation Location, double Accuracy);

    private sealed record GeolocateLocation(double Lat, double Lng);
}