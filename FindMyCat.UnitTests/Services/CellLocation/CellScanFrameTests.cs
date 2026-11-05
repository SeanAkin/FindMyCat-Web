using System.Buffers.Binary;
using FindMyCat.Core.Integrations.CellLocation;
using FindMyCat.Core.Integrations.Geolocation;

namespace FindMyCat.UnitTests.Services.CellLocation;

public sealed class CellScanFrameTests
{
    private static byte[] Frame(byte battery, params CellTower[] cellTowers)
    {
        var frame = new List<byte> { 0x02, (byte)cellTowers.Length, 0x10, 0x27, 0x00, 0x00 };
        foreach (var cellTower in cellTowers)
        {
            var cellBytes = new byte[12];
            BinaryPrimitives.WriteUInt16LittleEndian(cellBytes, (ushort)cellTower.MobileCountryCode);
            BinaryPrimitives.WriteUInt16LittleEndian(cellBytes.AsSpan(2), (ushort)cellTower.MobileNetworkCode);
            BinaryPrimitives.WriteUInt16LittleEndian(cellBytes.AsSpan(4), (ushort)cellTower.LocationAreaCode);
            BinaryPrimitives.WriteUInt32LittleEndian(cellBytes.AsSpan(6), (uint)cellTower.CellId);
            BinaryPrimitives.WriteInt16LittleEndian(cellBytes.AsSpan(10), (short)(cellTower.SignalStrength ?? 0));
            frame.AddRange(cellBytes);
        }

        frame.Add(battery);
        return frame.ToArray();
    }

    private static readonly CellTower ServingCell = new(21532831, 2862, 310, 410, -97);
    private static readonly CellTower NeighbourCell = new(21532832, 2862, 310, 410, -110);

    [Fact]
    public void Decodes_cells_and_battery()
    {
        var scan = CellScanFrame.Decode(Frame(87, ServingCell, NeighbourCell));

        scan.CellTowers.ShouldBe([ServingCell, NeighbourCell]);
        scan.BatteryPercent.ShouldBe(87);
    }

    [Fact]
    public void Zero_signal_strength_decodes_as_unknown()
    {
        var scan = CellScanFrame.Decode(Frame(87, ServingCell with { SignalStrength = null }));

        scan.CellTowers.Single().SignalStrength.ShouldBeNull();
    }

    [Fact]
    public void Unknown_battery_decodes_as_null()
    {
        CellScanFrame.Decode(Frame(0xFF, ServingCell)).BatteryPercent.ShouldBeNull();
    }

    [Fact]
    public void A_frame_without_the_battery_byte_is_rejected()
    {
        var frame = Frame(87, ServingCell);

        Should.Throw<GeolocationException>(() => CellScanFrame.Decode(frame[..^1]))
            .Message.ShouldStartWith("length mismatch");
    }

    [Fact]
    public void A_wifi_scan_frame_is_rejected()
    {
        var frame = Frame(87, ServingCell);
        frame[0] = 0x01;

        Should.Throw<GeolocationException>(() => CellScanFrame.Decode(frame))
            .Message.ShouldStartWith("not a cell scan frame");
    }
}