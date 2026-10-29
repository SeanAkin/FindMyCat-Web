using FindMyCat.Api.Extensions;
using FindMyCat.Api.Hubs;

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
    await app.SeedCredentialsAsync();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseWebSockets();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

var isTestingEnvironment = !app.Environment.IsEnvironment("Testing");
if (isTestingEnvironment)
{
    app.UseRateLimiter();
}

app.RejectCrossOriginLiveUpdateRequests();

app.MapControllers();
app.MapHub<DevicesHub>(DevicesHub.Route, options => options.CloseOnAuthenticationExpiration = true);
app.Map(TraccarSocketPassthrough.Route, (HttpContext context, TraccarSocketPassthrough passthrough) => passthrough.AcceptAsync(context));

app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// This is needed for WebApplicationFactory so IntegrationTests can se our entry point (https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0&pivots=xunit)
public partial class Program { }
