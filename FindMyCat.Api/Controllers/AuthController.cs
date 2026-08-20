using FindMyCat.Api.Auth;
using FindMyCat.Api.Contracts;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FindMyCat.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IUserProvisioningService userProvisioningService) : ControllerBase
{
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var redirectUri = "/";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            redirectUri = returnUrl;
        }

        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<SessionResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await userProvisioningService.RegisterWithPasswordAsync(
            request.Email, request.DisplayName, request.Password, cancellationToken);

        if (!result.IsSuccess)
        {
            return DenialResponse(result.DenialCode, result.DenialReason);
        }

        await SignInWithCookieAsync(result.User!);
        return Ok(SessionResponse.FromDomain(result.User!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<SessionResponse>> LoginWithPassword([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await userProvisioningService.SignInWithPasswordAsync(request.Email, request.Password, cancellationToken);
        if (!result.IsSuccess)
        {
            return Unauthorized(new AuthErrorResponse("invalid_credentials", "Incorrect email or password."));
        }

        await SignInWithCookieAsync(result.User!);
        return Ok(SessionResponse.FromDomain(result.User!));
    }

    [HttpGet("session")]
    public ActionResult<SessionResponse> Session() => Ok(SessionResponse.FromUser(User));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private Task SignInWithCookieAsync(User user) =>
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthClaimsFactory.CreatePrincipal(user));

    private ActionResult DenialResponse(string? code, string? message) => code switch
    {
        "weak_password" => BadRequest(new AuthErrorResponse(code, message!)),
        "email_already_registered" => Conflict(new AuthErrorResponse(code, message!)),
        _ => StatusCode(StatusCodes.Status403Forbidden, new AuthErrorResponse(code ?? "not_allow_listed", message ?? "Access denied.")),
    };
}
