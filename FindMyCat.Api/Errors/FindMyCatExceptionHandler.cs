using FindMyCat.Core.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace FindMyCat.Api.Errors;

internal sealed class FindMyCatExceptionHandler(ILogger<FindMyCatExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, error, logDetail) = exception switch
        {
            FindMyCatException known => ((int)known.Status, new ApiError(known.Code, known.Message, known.Errors), known.LogDetail),
            _ => (StatusCodes.Status500InternalServerError, StatusCodeErrors.For(StatusCodes.Status500InternalServerError), (string?)null)
        };

        logger.Log(
            statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Information,
            exception,
            "{Method} {Path} failed with {StatusCode} {ErrorCode}. Detail: {LogDetail}",
            httpContext.Request.Method, httpContext.Request.Path, statusCode, error.Code ?? "(no code)",
            logDetail ?? "(none)");

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        await ApiErrorResults.WriteAsync(httpContext.Response, statusCode, error, cancellationToken);
        return true;
    }
}
