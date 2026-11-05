using System.Text.Json;
using System.Text.Json.Nodes;
using FindMyCat.Core.Integrations.CellLocation;
using FindMyCat.Core.Integrations.Geolocation;
using FindMyCat.Core.Integrations.Traccar;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services;

public interface ICellLocator
{
    // True when the scan became a position in Traccar; either way the ping's outcome is recorded.
    Task<bool> LocateAsync(string uniqueId, string scanHex, CancellationToken cancellationToken = default);
}

public sealed class CellLocator(
    IGoogleGeolocationClient google,
    ITraccarPositionClient traccar,
    ITelemetryIngestService telemetry,
    ILogger<CellLocator> logger) : ICellLocator
{
    public async Task<bool> LocateAsync(string uniqueId, string scanHex, CancellationToken cancellationToken = default)
    {
        CellScan scan;
        GeoFix fix;
        try
        {
            scan = CellScanFrame.Decode(Convert.FromHexString(scanHex));
            if (scan.CellTowers.Count == 0)
            {
                throw new GeolocationException("no cells in the scan");
            }

            fix = await google.LocateAsync(scan.CellTowers, cancellationToken);
            await traccar.AddPositionAsync(uniqueId, fix, scan.BatteryPercent, cancellationToken);
        }
        catch (Exception ex) when (ex is GeolocationException or FormatException)
        {
            var reason = ex is FormatException ? "scan was not hex" : ex.Message;
            logger.LogWarning("Cell scan from {UniqueId} dropped: {Reason}", uniqueId, reason);
            await RecordPingAsync(uniqueId, new JsonObject { ["status"] = "fail", ["reason"] = reason }, cancellationToken);
            return false;
        }

        await RecordPingAsync(uniqueId, new JsonObject
        {
            ["status"] = "ok",
            ["cell_towers"] = scan.CellTowers.Count,
            ["accuracy_m"] = Math.Round(fix.AccuracyMeters)
        }, cancellationToken);
        return true;
    }

    // Same command name the collar uses when it reports a failed cell ping itself.
    private Task RecordPingAsync(string uniqueId, JsonObject fields, CancellationToken cancellationToken)
    {
        fields["kind"] = "command";
        fields["command"] = "ping-cell";
        fields["method"] = "cell";
        return telemetry.RecordStatusAsync(uniqueId, JsonSerializer.SerializeToElement(fields), cancellationToken);
    }
}