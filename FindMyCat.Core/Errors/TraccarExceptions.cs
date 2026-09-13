using System.Net;

namespace FindMyCat.Core.Errors;

public sealed class TraccarNotConfiguredException()
    : FindMyCatException(HttpStatusCode.Conflict, ErrorCodes.TraccarNotConfigured,
        "Traccar is not configured. Add your Traccar API token to view devices.");

public sealed class TraccarCredentialRejectedException(string? logDetail = null, Exception? innerException = null)
    : FindMyCatException(HttpStatusCode.Conflict, ErrorCodes.TraccarCredentialRejected,
        "Traccar rejected the stored token. Please re-enter your Traccar API token.", logDetail, innerException);

public sealed class TraccarUnavailableException(string? logDetail = null, Exception? innerException = null)
    : FindMyCatException(HttpStatusCode.BadGateway, ErrorCodes.TraccarUnavailable,
        "Traccar is currently unavailable. Please try again.", logDetail, innerException);

public sealed class DevicePositionNotFoundException(long deviceId)
    : FindMyCatException(HttpStatusCode.NotFound, ErrorCodes.DevicePositionNotFound,
        "No position has been reported for this collar yet.", logDetail: $"deviceId={deviceId}");

public sealed class InvalidHistoryRangeException()
    : FindMyCatException(HttpStatusCode.BadRequest, ErrorCodes.InvalidRange,
        "'from' must be earlier than 'to'.");

public sealed class HistoryRangeTooLargeException(TimeSpan maximum)
    : FindMyCatException(HttpStatusCode.BadRequest, ErrorCodes.RangeTooLarge,
        $"History range must not exceed {maximum.TotalDays:0} days.");
