namespace FindMyCat.Core.Integrations.Geolocation;

public sealed class GeolocationException(string reason) : Exception(reason);