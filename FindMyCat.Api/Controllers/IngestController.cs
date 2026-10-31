using FindMyCat.Api.Auth;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/ingest")]
[Authorize(AuthenticationSchemes = IngestApiKeyDefaults.Scheme)]
public class IngestController(ITelemetryIngestService telemetryIngestService, IWifiLocator wifiLocator) : ControllerBase
{
    [HttpPost("status")]
    public async Task<IActionResult> Status([FromBody] IngestStatusRequest request, CancellationToken cancellationToken)
    {
        await telemetryIngestService.RecordStatusAsync(request.Id, request.Payload, cancellationToken);
        return NoContent();
    }

    [HttpPost("wifi-scan")]
    public async Task<IActionResult> WifiScan([FromBody] IngestWifiScanRequest request, CancellationToken cancellationToken)
    {
        var located = await wifiLocator.LocateAsync(request.Id, request.Scan, cancellationToken);
        return located ? NoContent() : UnprocessableEntity();
    }
}
