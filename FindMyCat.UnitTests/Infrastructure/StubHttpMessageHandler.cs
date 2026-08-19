using System.Net;

namespace FindMyCat.UnitTests.Infrastructure;

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler ReturningJson(Func<string, (HttpStatusCode Status, string Body)> byAbsolutePath) =>
        new(request =>
        {
            var (status, body) = byAbsolutePath(request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request));
    }
}
