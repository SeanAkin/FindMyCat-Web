using System.Text.Json;
using System.Text.Json.Nodes;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.Integrations.WifiLocation;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services;

public interface IWifiLocator
{
    // True when the scan became a position in Traccar; either way the ping's outcome is recorded.
    Task<bool> LocateAsync(string uniqueId, string scanHex, CancellationToken cancellationToken = default);
}

public sealed class WifiLocator(
    IGoogleGeolocationClient google,
    ITraccarPositionClient traccar,
    ITelemetryIngestService telemetry,
    ILogger<WifiLocator> logger) : IWifiLocator
{
    // Google needs at least two access points to place a scan.
    private const int MinimumAccessPoints = 2;

    public async Task<bool> LocateAsync(string uniqueId, string scanHex, CancellationToken cancellationToken = default)
    {
        WifiScan scan;
        GeoFix fix;
        try
        {
            scan = WifiScanFrame.Decode(Convert.FromHexString(scanHex));
            if (scan.AccessPoints.Count < MinimumAccessPoints)
            {
                throw new WifiLocateException($"only {scan.AccessPoints.Count} access point(s)");
            }

            fix = await google.LocateAsync(scan.AccessPoints, cancellationToken);
            await traccar.AddPositionAsync(uniqueId, fix, scan.BatteryPercent, cancellationToken);
        }
        catch (Exception ex) when (ex is WifiLocateException or FormatException)
        {
            var reason = ex is FormatException ? "scan was not hex" : ex.Message;
            logger.LogWarning("WiFi scan from {UniqueId} dropped: {Reason}", uniqueId, reason);
            await RecordPingAsync(uniqueId, new JsonObject { ["status"] = "fail", ["reason"] = reason }, cancellationToken);
            return false;
        }

        await RecordPingAsync(uniqueId, new JsonObject
        {
            ["status"] = "ok",
            ["access_points"] = scan.AccessPoints.Count,
            ["accuracy_m"] = Math.Round(fix.AccuracyMeters)
        }, cancellationToken);
        return true;
    }

    private Task RecordPingAsync(string uniqueId, JsonObject fields, CancellationToken cancellationToken)
    {
        fields["kind"] = "command";
        fields["command"] = "ping";
        fields["method"] = "wifi";
        return telemetry.RecordStatusAsync(uniqueId, JsonSerializer.SerializeToElement(fields), cancellationToken);
    }
}