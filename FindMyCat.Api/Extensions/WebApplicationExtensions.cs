using FindMyCat.Api.Errors;
using FindMyCat.Api.Hubs;
using FindMyCat.Data;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseFindMyCatErrorResponses(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = context => ApiErrorResults.WriteAsync(
                context.Response, StatusCodes.Status500InternalServerError,
                StatusCodeErrors.For(StatusCodes.Status500InternalServerError))
        });

        app.UseStatusCodePages(context => ApiErrorResults.WriteAsync(
            context.HttpContext.Response,
            context.HttpContext.Response.StatusCode,
            StatusCodeErrors.For(context.HttpContext.Response.StatusCode)));

        return app;
    }

    public static WebApplication RejectCrossOriginHubRequests(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(DevicesHub.Route) && IsCrossOrigin(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });

        return app;
    }

    internal static bool IsCrossOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        var isNonBrowserClient = origin.Length == 0;
        if (isNonBrowserClient)
        {
            return false;
        }

        return !Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            || !Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var requestOriginUri)
            || Uri.Compare(originUri, requestOriginUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0;
    }

    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return app;
    }
}
