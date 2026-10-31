using System.Globalization;
using FindMyCat.Core.Integrations.WifiLocation;

namespace FindMyCat.Core.Integrations.Traccar;

public interface ITraccarPositionClient
{
    Task AddPositionAsync(string uniqueId, GeoFix fix, int? batteryPercent, CancellationToken cancellationToken = default);
}

public sealed class TraccarPositionClient(HttpClient httpClient) : ITraccarPositionClient
{
    public async Task AddPositionAsync(string uniqueId, GeoFix fix, int? batteryPercent, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = uniqueId,
            ["lat"] = fix.Latitude.ToString(CultureInfo.InvariantCulture),
            ["lon"] = fix.Longitude.ToString(CultureInfo.InvariantCulture),
            ["accuracy"] = fix.AccuracyMeters.ToString(CultureInfo.InvariantCulture)
        };
        if (batteryPercent is { } battery)
        {
            fields["batt"] = battery.ToString(CultureInfo.InvariantCulture);
        }

        try
        {
            using var response = await httpClient.PostAsync("", new FormUrlEncodedContent(fields), cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new WifiLocateException("location service unreachable");
        }
    }
}