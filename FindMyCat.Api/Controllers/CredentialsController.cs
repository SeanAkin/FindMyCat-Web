using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/credentials")]
public class CredentialsController(ICredentialService credentialService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CredentialStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await credentialService.GetStatusAsync(cancellationToken);
        return Ok(CredentialStatusResponse.FromDomain(status));
    }

    [HttpPut("traccar")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<IActionResult> SetTraccar(
        [FromBody] SetTraccarCredentialRequest request,
        CancellationToken cancellationToken)
    {
        await credentialService.SetTraccarTokenAsync(request.ApiToken, cancellationToken);
        return NoContent();
    }
    
    [HttpPut("hologram")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<IActionResult> SetHologram(
        [FromBody] SetHologramCredentialRequest request,
        CancellationToken cancellationToken)
    {
        await credentialService.SetHologramKeyAsync(request.ApiKey, cancellationToken);
        return NoContent();
    }

    [HttpDelete("traccar")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<IActionResult> DeleteTraccar(CancellationToken cancellationToken)
    {
        var removed = await credentialService.DeleteTraccarTokenAsync(cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpDelete("hologram")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<IActionResult> DeleteHologram(CancellationToken cancellationToken)
    {
        var removed = await credentialService.DeleteHologramKeyAsync(cancellationToken);
        return removed ? NoContent() : NotFound();
    }
}
