using System.Net;

namespace FindMyCat.Core.Errors;

public sealed class HologramNotConfiguredException()
    : FindMyCatException(HttpStatusCode.Conflict, ErrorCodes.HologramNotConfigured,
        "Hologram is not configured. Add a Hologram API key to control devices.");

public sealed class HologramDeviceNotFoundException(string deviceReference)
    : FindMyCatException(HttpStatusCode.NotFound, ErrorCodes.HologramDeviceNotFound,
        "Hologram does not recognise this collar.", logDetail: deviceReference);

public sealed class HologramCredentialRejectedException(string? logDetail = null, Exception? innerException = null)
    : FindMyCatException(HttpStatusCode.Conflict, ErrorCodes.HologramCredentialRejected,
        "Hologram rejected the stored API key. Please re-enter your Hologram API key.", logDetail, innerException);

public sealed class HologramUnavailableException(string? logDetail = null, Exception? innerException = null)
    : FindMyCatException(HttpStatusCode.BadGateway, ErrorCodes.HologramUnavailable,
        "Hologram is currently unavailable. Please try again.", logDetail, innerException);
