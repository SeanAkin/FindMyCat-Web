using FindMyCat.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddFindMyCatApi()
    .AddFindMyCatServices(builder.Configuration)
    .AddFindMyCatAuth(builder.Configuration)
    .AddFindMyCatForwardedHeaders()
    .AddFindMyCatRateLimiting();

var app = builder.Build();

app.UseFindMyCatErrorResponses();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.MigrateDatabase();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

var isTestingEnvironment = !app.Environment.IsEnvironment("Testing");
if (isTestingEnvironment)
{
    app.UseRateLimiter();
}

app.MapControllers();

app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// This is needed for WebApplicationFactory so IntegrationTests can se our entry point (https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0&pivots=xunit)
public partial class Program { }
