using FindMyCat.Api.Contracts;
using FindMyCat.Api.Filters;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.Core.Services.Traccar;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/devices")]
[TypeFilter(typeof(TraccarExceptionFilter))]
[TypeFilter(typeof(HologramExceptionFilter))]
public class DevicesController(ITraccarService traccarService, IHologramService hologramService) : ControllerBase
{
    private static readonly TimeSpan MaxHistoryRange = TimeSpan.FromDays(31);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> GetDevices(CancellationToken cancellationToken)
    {
        var devices = await traccarService.GetDevicesAsync(cancellationToken);
        return Ok(devices.Select(DeviceResponse.FromDomain).ToList());
    }

    [HttpGet("{deviceId:long}/position")]
    public async Task<ActionResult<PositionResponse>> GetPosition(long deviceId, CancellationToken cancellationToken)
    {
        var position = await traccarService.GetLatestPositionAsync(deviceId, cancellationToken);
        return position is null ? NotFound() : Ok(PositionResponse.FromDomain(position));
    }

    [HttpGet("{deviceId:long}/history")]
    public async Task<ActionResult<IReadOnlyList<PositionResponse>>> GetHistory(
        long deviceId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (from >= to)
        {
            return BadRequest(new TraccarErrorResponse("invalid_range", "'from' must be earlier than 'to'."));
        }

        if (to - from > MaxHistoryRange)
        {
            return BadRequest(new TraccarErrorResponse(
                "range_too_large", $"History range must not exceed {MaxHistoryRange.TotalDays:0} days."));
        }

        var history = await traccarService.GetHistoryAsync(deviceId, from, to, cancellationToken);
        return Ok(history.Select(PositionResponse.FromDomain).ToList());
    }

    [HttpPost("{deviceId:long}/ping")]
    public async Task<IActionResult> Ping(long deviceId, CancellationToken cancellationToken)
    {
        await hologramService.SendCommandAsync(deviceId, HologramCommand.Ping, cancellationToken);
        return NoContent();
    }

    [HttpPost("{deviceId:long}/lost")]
    public async Task<IActionResult> MarkLost(long deviceId, CancellationToken cancellationToken)
    {
        await hologramService.SendCommandAsync(deviceId, HologramCommand.Lost, cancellationToken);
        return NoContent();
    }

    [HttpPost("{deviceId:long}/active")]
    public async Task<IActionResult> MarkActive(long deviceId, CancellationToken cancellationToken)
    {
        await hologramService.SendCommandAsync(deviceId, HologramCommand.Active, cancellationToken);
        return NoContent();
    }
}
