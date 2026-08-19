namespace FindMyCat.IntegrationTests.Infrastructure;

public sealed class TestResponseLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var output = IntegrationTestBase.CurrentOutput;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            output?.WriteLine($"--- {(int)response.StatusCode} {response.ReasonPhrase} ---");
            output?.WriteLine($"{request.Method} {request.RequestUri}");

            if (!string.IsNullOrWhiteSpace(body))
            {
                output?.WriteLine("Response body:");
                output?.WriteLine(body);
            }
        }

        return response;
    }
}
