using FindMyCat.Api.Contracts;
using FindMyCat.Core.Services.Traccar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FindMyCat.Api.Filters;

public sealed class TraccarExceptionFilter : IExceptionFilter
{
    // Not sure how I feel about this right now. TODO: Come back and revisit exception filters. 
    // Initial thoughts: Error codes to enums, maybe a generic exception middleware instead to handle error messages?
    // Standardising API response messages?
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case TraccarNotConfiguredException:
                context.Result = Error(StatusCodes.Status409Conflict, "traccar_not_configured",
                    "Traccar is not configured. Add your Traccar API token to view devices.");
                context.ExceptionHandled = true;
                break;

            case TraccarUpstreamException { CredentialRejected: true }:
                context.Result = Error(StatusCodes.Status409Conflict, "traccar_credential_rejected",
                    "Traccar rejected the stored token. Please re-enter your Traccar API token.");
                context.ExceptionHandled = true;
                break;

            case TraccarUpstreamException:
                context.Result = Error(StatusCodes.Status502BadGateway, "traccar_unavailable",
                    "Traccar is currently unavailable. Please try again.");
                context.ExceptionHandled = true;
                break;
        }
    }

    private static ObjectResult Error(int statusCode, string code, string message) =>
        new(new TraccarErrorResponse(code, message)) { StatusCode = statusCode };
}
