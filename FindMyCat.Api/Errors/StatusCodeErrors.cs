namespace FindMyCat.Api.Errors;

/// <summary>
/// Bodies for failures the status code already describes. These carry no error code on purpose:
/// there is nothing for a client to branch on that <c>response.status</c> does not already tell it.
/// </summary>
internal static class StatusCodeErrors
{
    public static ApiError For(int statusCode) => new(Code: null, MessageFor(statusCode));

    private static string MessageFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => "You need to sign in to do that.",
        StatusCodes.Status403Forbidden => "You do not have permission to do that.",
        StatusCodes.Status404NotFound => "That resource does not exist.",
        StatusCodes.Status429TooManyRequests => "Too many attempts. Please wait a moment and try again.",
        >= StatusCodes.Status500InternalServerError => "Something went wrong. Please try again.",
        _ => "The request was not valid."
    };
}
