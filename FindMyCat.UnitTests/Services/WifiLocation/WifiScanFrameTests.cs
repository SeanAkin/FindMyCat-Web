using FindMyCat.Core.Integrations.WifiLocation;

namespace FindMyCat.UnitTests.Services.WifiLocation;

public sealed class WifiScanFrameTests
{
    private static byte[] Frame(byte battery, params (byte[] Bssid, sbyte Rssi)[] accessPoints)
    {
        var frame = new List<byte> { 0x01, (byte)accessPoints.Length, 0x10, 0x27, 0x00, 0x00 };
        foreach (var (bssid, rssi) in accessPoints)
        {
            frame.AddRange(bssid);
            frame.Add((byte)rssi);
        }

        frame.Add(battery);
        return frame.ToArray();
    }

    private static readonly byte[] Home = [0xaa, 0xbb, 0xcc, 0x00, 0x11, 0x22];
    private static readonly byte[] Office = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

    [Fact]
    public void Decodes_access_points_and_battery()
    {
        var scan = WifiScanFrame.Decode(Frame(87, (Home, -48), (Office, -71)));

        scan.AccessPoints.ShouldBe(
        [
            new WifiAccessPoint("aa:bb:cc:00:11:22", -48),
            new WifiAccessPoint("01:02:03:04:05:06", -71)
        ]);
        scan.BatteryPercent.ShouldBe(87);
    }

    [Fact]
    public void Unknown_battery_decodes_as_null()
    {
        WifiScanFrame.Decode(Frame(0xFF, (Home, -48))).BatteryPercent.ShouldBeNull();
    }

    [Fact]
    public void A_frame_without_the_battery_byte_is_rejected()
    {
        var frame = Frame(87, (Home, -48));

        Should.Throw<WifiLocateException>(() => WifiScanFrame.Decode(frame[..^1]))
            .Message.ShouldStartWith("length mismatch");
    }

    [Fact]
    public void Another_message_type_is_rejected()
    {
        var frame = Frame(87, (Home, -48));
        frame[0] = 0x02;

        Should.Throw<WifiLocateException>(() => WifiScanFrame.Decode(frame))
            .Message.ShouldStartWith("not a scan frame");
    }

    [Fact]
    public void A_truncated_header_is_rejected()
    {
        Should.Throw<WifiLocateException>(() => WifiScanFrame.Decode([0x01, 0x02]));
    }
}