using System.Text.Json.Nodes;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Integrations.Hologram;
using FindMyCat.Core.Integrations.Traccar;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(
    ITraccarService traccarService,
    IHologramService hologramService,
    IDeviceEventRepository deviceEventRepository) : ControllerBase
{
    private const int DefaultEventLimit = 20;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> GetDevices(CancellationToken cancellationToken)
    {
        var devices = await traccarService.GetDevicesAsync(cancellationToken);
        return Ok(devices.Select(DeviceResponse.FromDomain).ToList());
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<DeviceResponse>> CreateDevice(
        [FromBody] SaveDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await traccarService.CreateDeviceAsync(
            request.Name, request.UniqueId, request.Attributes!.Emoji!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, DeviceResponse.FromDomain(device));
    }

    [HttpPut("{deviceId:long}")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<DeviceResponse>> UpdateDevice(
        long deviceId, [FromBody] SaveDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await traccarService.UpdateDeviceAsync(
            deviceId, request.Name, request.UniqueId, request.Attributes!.Emoji!, cancellationToken);
        return Ok(DeviceResponse.FromDomain(device));
    }

    [HttpDelete("{deviceId:long}")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<IActionResult> DeleteDevice(long deviceId, CancellationToken cancellationToken)
    {
        await traccarService.DeleteDeviceAsync(deviceId, cancellationToken);
        return NoContent();
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

    [HttpGet("{deviceId:long}/state")]
    public async Task<ActionResult<JsonObject>> GetState(long deviceId, CancellationToken cancellationToken)
    {
        var uniqueId = await ResolveUniqueIdAsync(deviceId, cancellationToken);
        var events = await deviceEventRepository.GetLatestPerKindAsync(uniqueId, cancellationToken);
        return Ok(DeviceEventResponse.State(events));
    }

    [HttpGet("{deviceId:long}/events")]
    public async Task<ActionResult<IReadOnlyList<JsonObject>>> GetEvents(
        long deviceId,
        [FromQuery] DeviceEventsRequest request,
        CancellationToken cancellationToken)
    {
        var uniqueId = await ResolveUniqueIdAsync(deviceId, cancellationToken);
        var events = await deviceEventRepository.GetEventsAsync(
            uniqueId, request.After ?? 0, request.Kind, request.Limit ?? DefaultEventLimit, cancellationToken);
        return Ok(events.Select(DeviceEventResponse.Event).ToList());
    }

    private async Task<string> ResolveUniqueIdAsync(long deviceId, CancellationToken cancellationToken)
    {
        var devices = await traccarService.GetDevicesAsync(cancellationToken);
        var device = devices.FirstOrDefault(d => d.Id == deviceId)
                     ?? throw new DeviceNotFoundException(deviceId);
        return device.UniqueId;
    }

    [HttpPost("{deviceId:long}/command/{command}")]
    public async Task<IActionResult> SendCommand(long deviceId, string command, CancellationToken cancellationToken)
    {
        if (!HologramCommandExtensions.TryParse(command, out var hologramCommand))
        {
            return NotFound();
        }

        await hologramService.SendCommandAsync(deviceId, hologramCommand, cancellationToken);
        return NoContent();
    }
}
