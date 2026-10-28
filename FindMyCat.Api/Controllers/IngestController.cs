using FindMyCat.Api.Auth;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/ingest")]
[Authorize(AuthenticationSchemes = IngestApiKeyDefaults.Scheme)]
public class IngestController(ITelemetryIngestService telemetryIngestService) : ControllerBase
{
    [HttpPost("status")]
    public async Task<IActionResult> Status([FromBody] IngestStatusRequest request, CancellationToken cancellationToken)
    {
        await telemetryIngestService.RecordStatusAsync(request.Id, request.Payload, cancellationToken);
        return NoContent();
    }
}
