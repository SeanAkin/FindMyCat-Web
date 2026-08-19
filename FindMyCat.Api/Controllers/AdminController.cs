using System.Security.Claims;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Administrator))]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("allowed-emails")]
    public async Task<ActionResult<IReadOnlyList<AllowedEmailResponse>>> GetAllowedEmails(CancellationToken cancellationToken)
    {
        var allowedEmails = await adminService.ListAllowedEmailsAsync(cancellationToken);
        return Ok(allowedEmails.Select(AllowedEmailResponse.FromDomain));
    }

    [HttpPost("allowed-emails")]
    public async Task<ActionResult<AllowedEmailResponse>> AddAllowedEmail(
        [FromBody] AddAllowedEmailRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var added = await adminService.AddAllowedEmailAsync(request.Email, currentUserId, cancellationToken);
        return Ok(AllowedEmailResponse.FromDomain(added));
    }

    [HttpDelete("allowed-emails/{email}")]
    public async Task<IActionResult> RemoveAllowedEmail(string email, CancellationToken cancellationToken)
    {
        var result = await adminService.RemoveAllowedEmailAsync(email, cancellationToken);
        return result switch
        {
            RemoveAllowedEmailResult.Removed => NoContent(),
            RemoveAllowedEmailResult.NotFound => NotFound(),
            RemoveAllowedEmailResult.PrimaryAdministratorProtected => Conflict(new AdminErrorResponse(
                "primary_administrator_protected",
                "The original administrator account's email cannot be removed from the allow-list.")),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RemoveAllowedEmailResult)}: {result}.")
        };
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await adminService.ListUsersAsync(cancellationToken);
        return Ok(users.Select(UserResponse.FromDomain));
    }

    [HttpPut("users/{id:guid}/role")]
    public async Task<IActionResult> SetUserRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await adminService.SetUserRoleAsync(id, request.Role, cancellationToken);
        return result switch
        {
            SetUserRoleResult.Success => NoContent(),
            SetUserRoleResult.UserNotFound => NotFound(),
            SetUserRoleResult.PrimaryAdministratorProtected => Conflict(new AdminErrorResponse(
                "primary_administrator_protected",
                "The original administrator account's role cannot be changed.")),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SetUserRoleResult)}: {result}.")
        };
    }
}
