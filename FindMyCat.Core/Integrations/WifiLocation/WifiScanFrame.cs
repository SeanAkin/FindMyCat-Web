namespace FindMyCat.Core.Integrations.WifiLocation;

public sealed record WifiAccessPoint(string MacAddress, int SignalStrength);

public sealed record WifiScan(IReadOnlyList<WifiAccessPoint> AccessPoints, int? BatteryPercent);

public sealed class WifiLocateException(string reason) : Exception(reason);

// Mirrors WiFi/wifi_scan.h on the collar.
public static class WifiScanFrame
{
    private const byte MessageType = 0x01;
    private const int HeaderBytes = 6; // type, count, uptime (u32 LE)
    private const int BytesPerAccessPoint = 7; // BSSID, RSSI (i8)
    private const int TrailerBytes = 1; // battery percent
    private const byte BatteryUnknown = 0xFF;

    public static WifiScan Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < HeaderBytes || frame[0] != MessageType)
        {
            throw new WifiLocateException($"not a scan frame ({frame.Length} bytes)");
        }

        var count = frame[1];
        var batteryOffset = HeaderBytes + count * BytesPerAccessPoint;
        if (frame.Length != batteryOffset + TrailerBytes)
        {
            throw new WifiLocateException($"length mismatch: count {count}, {frame.Length} bytes");
        }

        var accessPoints = new List<WifiAccessPoint>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = frame.Slice(HeaderBytes + i * BytesPerAccessPoint, BytesPerAccessPoint);
            var bssid = string.Join(':', entry[..6].ToArray().Select(b => b.ToString("x2")));
            accessPoints.Add(new WifiAccessPoint(bssid, (sbyte)entry[6]));
        }

        var battery = frame[batteryOffset];
        return new WifiScan(accessPoints, battery == BatteryUnknown ? null : battery);
    }
}