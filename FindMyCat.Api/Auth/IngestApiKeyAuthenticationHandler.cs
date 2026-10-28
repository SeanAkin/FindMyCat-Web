using System.Security.Claims;
using System.Text.Encodings.Web;
using FindMyCat.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FindMyCat.Api.Auth;

public static class IngestApiKeyDefaults
{
    public const string Scheme = "IngestApiKey";

    public const string HeaderName = "X-Api-Key";
}

public sealed class IngestApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICredentialService credentialService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(IngestApiKeyDefaults.HeaderName, out var provided) || provided.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        if (!await credentialService.ValidateIngestKeyAsync(provided.ToString(), Context.RequestAborted))
        {
            return AuthenticateResult.Fail("The ingest API key is missing or invalid.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, IngestApiKeyDefaults.Scheme)], IngestApiKeyDefaults.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), IngestApiKeyDefaults.Scheme);

        return AuthenticateResult.Success(ticket);
    }
}
