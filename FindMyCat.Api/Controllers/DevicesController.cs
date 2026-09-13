using FindMyCat.Api.Contracts;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Hologram;
using FindMyCat.Core.Integrations.Traccar;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(ITraccarService traccarService, IHologramService hologramService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> GetDevices(CancellationToken cancellationToken)
    {
        var devices = await traccarService.GetDevicesAsync(cancellationToken);
        return Ok(devices.Select(DeviceResponse.FromDomain).ToList());
    }

    [HttpGet("{deviceId:long}/position")]
    public async Task<ActionResult<PositionResponse>> GetPosition(long deviceId, CancellationToken cancellationToken)
    {
        var position = await traccarService.GetLatestPositionAsync(deviceId, cancellationToken)
                       ?? throw new DevicePositionNotFoundException(deviceId);

        return Ok(PositionResponse.FromDomain(position));
    }

    [HttpGet("{deviceId:long}/history")]
    public async Task<ActionResult<IReadOnlyList<PositionResponse>>> GetHistory(
        long deviceId,
        [FromQuery] HistoryRangeRequest request,
        CancellationToken cancellationToken)
    {
        var history = await traccarService.GetHistoryAsync(
            deviceId, request.From!.Value, request.To!.Value, cancellationToken);
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
