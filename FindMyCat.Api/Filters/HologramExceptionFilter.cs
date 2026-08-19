using FindMyCat.Api.Contracts;
using FindMyCat.Core.Services.Hologram;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FindMyCat.Api.Filters;

public sealed class HologramExceptionFilter : IExceptionFilter
{
    // Not sure how I feel about this right now. TODO: Come back and revisit exception filters. 
    // Initial thoughts: Error codes to enums, maybe a generic exception middleware instead to handle error messages?
    // Standardising API response messages?
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case HologramNotConfiguredException:
                context.Result = Error(StatusCodes.Status409Conflict, "hologram_not_configured",
                    "Hologram is not configured. Add a Hologram API key to control devices.");
                context.ExceptionHandled = true;
                break;

            case HologramDeviceNotFoundException ex:
                context.Result = Error(StatusCodes.Status404NotFound, "hologram_device_not_found", ex.Message);
                context.ExceptionHandled = true;
                break;

            case HologramUpstreamException { CredentialRejected: true }:
                context.Result = Error(StatusCodes.Status409Conflict, "hologram_credential_rejected",
                    "Hologram rejected the stored API key. Please re-enter your Hologram API key.");
                context.ExceptionHandled = true;
                break;

            case HologramUpstreamException:
                context.Result = Error(StatusCodes.Status502BadGateway, "hologram_unavailable",
                    "Hologram is currently unavailable. Please try again.");
                context.ExceptionHandled = true;
                break;
        }
    }

    private static ObjectResult Error(int statusCode, string code, string message) =>
        new(new TraccarErrorResponse(code, message)) { StatusCode = statusCode };
}
