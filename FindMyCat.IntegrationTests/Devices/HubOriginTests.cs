using FindMyCat.Api.Extensions;
using Microsoft.AspNetCore.Http;

namespace FindMyCat.IntegrationTests.Devices;

public sealed class HubOriginTests
{
    private static HttpRequest RequestTo(string scheme, string host, string? origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        return context.Request;
    }

    [Theory]
    [InlineData("https", "cats.example.com", "https://cats.example.com")]
    [InlineData("https", "cats.example.com:443", "https://cats.example.com")]
    [InlineData("https", "CATS.example.com", "https://cats.example.com")]
    [InlineData("http", "localhost:5173", "http://localhost:5173")]
    [InlineData("https", "cats.example.com", null)]
    public void Same_origin_or_non_browser_requests_are_allowed(string scheme, string host, string? origin)
    {
        WebApplicationExtensions.IsCrossOrigin(RequestTo(scheme, host, origin)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("https", "cats.example.com", "https://evil.example.com")]
    [InlineData("https", "cats.example.com", "http://cats.example.com")]
    [InlineData("http", "localhost:5173", "http://localhost:5174")]
    [InlineData("https", "cats.example.com", "null")]
    public void Cross_origin_requests_are_rejected(string scheme, string host, string origin)
    {
        WebApplicationExtensions.IsCrossOrigin(RequestTo(scheme, host, origin)).ShouldBeTrue();
    }
}
