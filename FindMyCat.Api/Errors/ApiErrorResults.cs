using FindMyCat.Api.Json;

namespace FindMyCat.Api.Errors;

public static class ApiErrorResults
{
    public static Task WriteAsync(HttpResponse response, int statusCode, ApiError error, CancellationToken cancellationToken = default)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(error, ApiJsonOptions.Default, cancellationToken);
    }
}
