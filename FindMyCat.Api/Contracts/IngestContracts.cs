using System.Text.Json;

namespace FindMyCat.Api.Contracts;

public sealed record IngestStatusRequest(string Id, JsonElement Payload);

public sealed record IngestWifiScanRequest(string Id, string Scan);

public sealed record IngestCellScanRequest(string Id, string Scan);

public sealed record IngestGnssFixRequest(string Id, string Fix);
