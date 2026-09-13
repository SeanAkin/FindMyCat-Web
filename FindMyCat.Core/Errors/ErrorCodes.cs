using System.Reflection;

namespace FindMyCat.Core.Errors;

public static class ErrorCodes
{
    public const string NotAllowListed = "not_allow_listed";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string EmailRegisteredWithPassword = "email_registered_with_password";
    public const string WeakPassword = "weak_password";
    public const string InvalidCredentials = "invalid_credentials";

    public const string PrimaryAdministratorProtected = "primary_administrator_protected";
    public const string AllowedEmailNotFound = "allowed_email_not_found";
    public const string UserNotFound = "user_not_found";
    public const string CredentialNotConfigured = "credential_not_configured";

    public const string DevicePositionNotFound = "device_position_not_found";
    public const string InvalidRange = "invalid_range";
    public const string RangeTooLarge = "range_too_large";

    public const string TraccarNotConfigured = "traccar_not_configured";
    public const string TraccarCredentialRejected = "traccar_credential_rejected";
    public const string TraccarUnavailable = "traccar_unavailable";

    public const string HologramNotConfigured = "hologram_not_configured";
    public const string HologramDeviceNotFound = "hologram_device_not_found";
    public const string HologramCredentialRejected = "hologram_credential_rejected";
    public const string HologramUnavailable = "hologram_unavailable";

    public static IReadOnlyList<string> All { get; } = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
