using System.Buffers.Binary;
using System.Text.Json.Serialization;
using FindMyCat.Core.Integrations.Geolocation;

namespace FindMyCat.Core.Integrations.CellLocation;

// Property names are Google's geolocation cellTowers fields.
public sealed record CellTower(
    long CellId,
    int LocationAreaCode,
    int MobileCountryCode,
    int MobileNetworkCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SignalStrength);

public sealed record CellScan(IReadOnlyList<CellTower> CellTowers, int? BatteryPercent);

// Mirrors CellScan/cell_scan.h on the collar.
public static class CellScanFrame
{
    private const byte MessageType = 0x02;
    private const int HeaderLengthBytes = 6; // type, count, uptime (u32 LE)
    private const int LengthBytesPerCell = 12; // MCC, MNC, TAC (u16), cell ID (u32), RSRP dBm (i16)
    private const int TrailerLengthBytes = 1; // battery percent
    private const byte BatteryUnknown = 0xFF;

    public static CellScan Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < HeaderLengthBytes || frame[0] != MessageType)
        {
            throw new GeolocationException($"not a cell scan frame ({frame.Length} bytes)");
        }

        var cellCount = frame[1];
        var batteryOffset = HeaderLengthBytes + cellCount * LengthBytesPerCell;
        if (frame.Length != batteryOffset + TrailerLengthBytes)
        {
            throw new GeolocationException($"length mismatch: count {cellCount}, {frame.Length} bytes");
        }

        var cellTowers = new List<CellTower>(cellCount);
        for (var i = 0; i < cellCount; i++)
        {
            var cellBytes = frame.Slice(HeaderLengthBytes + i * LengthBytesPerCell, LengthBytesPerCell);
            var signalStrengthDbm = BinaryPrimitives.ReadInt16LittleEndian(cellBytes[10..]);
            cellTowers.Add(new CellTower(
                CellId: BinaryPrimitives.ReadUInt32LittleEndian(cellBytes[6..]),
                LocationAreaCode: BinaryPrimitives.ReadUInt16LittleEndian(cellBytes[4..]),
                MobileCountryCode: BinaryPrimitives.ReadUInt16LittleEndian(cellBytes),
                MobileNetworkCode: BinaryPrimitives.ReadUInt16LittleEndian(cellBytes[2..]),
                SignalStrength: signalStrengthDbm == 0 ? null : signalStrengthDbm));
        }

        var battery = frame[batteryOffset];
        return new CellScan(cellTowers, battery == BatteryUnknown ? null : battery);
    }
}