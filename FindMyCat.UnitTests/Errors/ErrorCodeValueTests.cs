using System.Reflection;
using FindMyCat.Core.Errors;

namespace FindMyCat.UnitTests.Errors;

public sealed class ErrorCodeValueTests
{
    [Fact]
    public void Every_error_code_keeps_the_value_the_client_branches_on()
    {
        foreach (var constant in ErrorCodeConstants())
        {
            var value = (string)constant.GetRawConstantValue()!;

            value.ShouldBe(ExpectedValueFor(constant.Name), $"{constant.Name} changed the value it sends.");
        }
    }

    private static string ExpectedValueFor(string constantName) => constantName switch
    {
        nameof(ErrorCodes.NotAllowListed) => "not_allow_listed",
        nameof(ErrorCodes.EmailAlreadyRegistered) => "email_already_registered",
        nameof(ErrorCodes.EmailRegisteredWithPassword) => "email_registered_with_password",
        nameof(ErrorCodes.WeakPassword) => "weak_password",
        nameof(ErrorCodes.InvalidCredentials) => "invalid_credentials",
        nameof(ErrorCodes.PrimaryAdministratorProtected) => "primary_administrator_protected",
        nameof(ErrorCodes.AllowedEmailNotFound) => "allowed_email_not_found",
        nameof(ErrorCodes.UserNotFound) => "user_not_found",
        nameof(ErrorCodes.CredentialNotConfigured) => "credential_not_configured",
        nameof(ErrorCodes.DevicePositionNotFound) => "device_position_not_found",
        nameof(ErrorCodes.InvalidRange) => "invalid_range",
        nameof(ErrorCodes.RangeTooLarge) => "range_too_large",
        nameof(ErrorCodes.TraccarNotConfigured) => "traccar_not_configured",
        nameof(ErrorCodes.TraccarCredentialRejected) => "traccar_credential_rejected",
        nameof(ErrorCodes.TraccarUnavailable) => "traccar_unavailable",
        nameof(ErrorCodes.HologramNotConfigured) => "hologram_not_configured",
        nameof(ErrorCodes.HologramDeviceNotFound) => "hologram_device_not_found",
        nameof(ErrorCodes.HologramCredentialRejected) => "hologram_credential_rejected",
        nameof(ErrorCodes.HologramUnavailable) => "hologram_unavailable",
        _ => throw new ArgumentOutOfRangeException(
            nameof(constantName), constantName,
            "New error code: add the value it sends here, and to the ApiErrorCode union in frontend/src/api/types.ts.")
    };

    private static IEnumerable<FieldInfo> ErrorCodeConstants() => typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string));
}
