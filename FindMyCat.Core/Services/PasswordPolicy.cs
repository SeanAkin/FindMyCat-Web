namespace FindMyCat.Core.Services;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 64;

    public static bool IsValid(string password) => GetViolations(password).Count == 0;

    public static IReadOnlyList<string> GetViolations(string password)
    {
        var violations = new List<string>();

        if (password.Length < MinimumLength)
        {
            violations.Add($"Password must be at least {MinimumLength} characters long.");
        }

        if (password.Length > MaximumLength)
        {
            violations.Add($"Password must be at most {MaximumLength} characters long.");
        }

        if (!password.Any(char.IsUpper))
        {
            violations.Add("Password must contain at least one uppercase letter.");
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            violations.Add("Password must contain at least one symbol.");
        }

        return violations;
    }
}
