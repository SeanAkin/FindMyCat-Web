using System.Security.Authentication;
using System.Security.Claims;
using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Models;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace FindMyCat.Api.Auth;

internal sealed class GoogleAuthenticationEvents(IUserProvisioningService provisioningService) : OAuthEvents
{
    private const string SignInDenialCodeItemsKey = "FindMyCat.SignInDenialCode";

    // context.Fail is silently ignored by OAuthHandler.CreateTicketAsync; throwing is the
    // only way to abort ticket creation - protects the fix for the auth bypass from being
    // refactored away.
    public override async Task CreatingTicket(OAuthCreatingTicketContext context)
    {
        var googleSubjectId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        var displayName = context.Principal?.FindFirstValue(ClaimTypes.Name) ?? email;

        if (string.IsNullOrWhiteSpace(googleSubjectId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new AuthenticationException("Google did not return the expected profile information.");
        }

        User user;
        try
        {
            user = await provisioningService.ProvisionOrSignInAsync(
                new GoogleUserInfo(googleSubjectId, email, displayName),
                context.HttpContext.RequestAborted);
        }
        catch (FindMyCatException ex)
        {
            context.HttpContext.Items[SignInDenialCodeItemsKey] = ex.Code;
            throw new AuthenticationException(ex.Message, ex);
        }

        context.Principal = AuthClaimsFactory.CreatePrincipal(user);
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        context.HandleResponse();
        var code = context.HttpContext.Items[SignInDenialCodeItemsKey] as string ?? "sign_in_failed";
        context.Response.Redirect($"/login?error={code}");
        return Task.CompletedTask;
    }
}
