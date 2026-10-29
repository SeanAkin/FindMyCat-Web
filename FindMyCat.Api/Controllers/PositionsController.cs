using FindMyCat.Api.Contracts;
using FindMyCat.Core.Integrations.Traccar;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

// Mirrors Traccar's GET /api/positions: the latest position of every device.
[ApiController]
[Route("api/positions")]
public class PositionsController(ITraccarService traccarService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PositionResponse>>> GetLatestPositions(CancellationToken cancellationToken)
    {
        var devices = await traccarService.GetDevicesAsync(cancellationToken);
        return Ok(devices
            .Where(d => d.LatestPosition is not null)
            .Select(d => PositionResponse.FromDomain(d.LatestPosition!))
            .ToList());
    }
}