using FindMyCat.Api.Errors;
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

    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return app;
    }
}
