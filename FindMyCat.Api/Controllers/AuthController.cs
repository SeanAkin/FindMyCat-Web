using FindMyCat.Api.Auth;
using FindMyCat.Api.Contracts;
using FindMyCat.Api.Errors;
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
public class AuthController(IUserProvisioningService userProvisioningService, GoogleAuthSettings googleAuthSettings) : ControllerBase
{
    [HttpGet("providers")]
    [AllowAnonymous]
    public ActionResult<AuthProvidersResponse> Providers()
    {
        var providers = new List<AuthProvider> { AuthProvider.Password };
        if (googleAuthSettings.Enabled)
        {
            providers.Add(AuthProvider.Google);
        }

        return Ok(new AuthProvidersResponse(providers));
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        if (!googleAuthSettings.Enabled)
        {
            return NotFound(new ApiError(Code: null, "Google sign-in is not enabled."));
        }

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
        var user = await userProvisioningService.RegisterWithPasswordAsync(
            request.Email, request.DisplayName, request.Password, cancellationToken);

        await SignInWithCookieAsync(user);
        return Ok(SessionResponse.FromDomain(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<SessionResponse>> LoginWithPassword([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userProvisioningService.SignInWithPasswordAsync(request.Email, request.Password, cancellationToken);

        await SignInWithCookieAsync(user);
        return Ok(SessionResponse.FromDomain(user));
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
}
