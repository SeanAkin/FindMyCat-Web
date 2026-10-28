using System.Net;

namespace FindMyCat.Core.Errors;

public sealed class TelemetryPayloadException(string logDetail, Exception? innerException = null)
    : FindMyCatException(HttpStatusCode.UnprocessableEntity, ErrorCodes.InvalidTelemetryPayload,
        "The telemetry payload could not be read.", logDetail, innerException);
