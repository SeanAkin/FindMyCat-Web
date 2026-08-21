using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.AspNetCore.Identity;

namespace FindMyCat.Core.Services;

public interface IUserProvisioningService
{
    Task<UserProvisioningResult> ProvisionOrSignInAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken = default);

    Task<UserProvisioningResult> RegisterWithPasswordAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);

    Task<PasswordSignInResult> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken = default);
}

public sealed class UserProvisioningService(
    IUserRepository userRepository,
    IAllowedEmailRepository allowedEmailRepository,
    IPasswordHasher<User> passwordHasher) : IUserProvisioningService
{
    public async Task<UserProvisioningResult> ProvisionOrSignInAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken = default)
    {
        var email = EmailNormalizer.Normalize(googleUser.Email);

        var existing = await userRepository.GetByGoogleSubjectIdAsync(googleUser.GoogleSubjectId, cancellationToken);
        if (existing is not null)
        {
            await userRepository.UpdateLastLoginAsync(existing.Id, DateTimeOffset.UtcNow, cancellationToken);
            return UserProvisioningResult.Success(existing);
        }

        var emailOwner = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (emailOwner is not null)
        {
            return UserProvisioningResult.Denied(
                "This email already has a password-based account. Sign in with your email and password instead.",
                "email_registered_with_password");
        }

        // First user = Primary Admin
        var anyUsersExist = await userRepository.AnyAsync(cancellationToken);
        if (!anyUsersExist)
        {
            var admin = await CreateGoogleUserAsync(
                email, googleUser.DisplayName, googleUser.GoogleSubjectId,
                UserRole.Administrator, isPrimaryAdministrator: true, cancellationToken);
            return UserProvisioningResult.Success(admin);
        }

        var isAllowListed = await allowedEmailRepository.IsAllowedAsync(email, cancellationToken);
        if (!isAllowListed)
        {
            return UserProvisioningResult.Denied("This email has not been added to the allowed list.");
        }

        var user = await CreateGoogleUserAsync(
            email, googleUser.DisplayName, googleUser.GoogleSubjectId,
            UserRole.User, isPrimaryAdministrator: false, cancellationToken);
        return UserProvisioningResult.Success(user);
    }

    public async Task<UserProvisioningResult> RegisterWithPasswordAsync(
        string email, string displayName, string password, CancellationToken cancellationToken = default)
    {
        email = EmailNormalizer.Normalize(email);

        var passwordViolations = PasswordPolicy.GetViolations(password);
        if (passwordViolations.Count > 0)
        {
            return UserProvisioningResult.Denied(string.Join(' ', passwordViolations), "weak_password");
        }

        var emailOwner = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (emailOwner is not null)
        {
            return UserProvisioningResult.Denied(
                "An account with this email already exists.", "email_already_registered");
        }

        // First user = Primary Admin, regardless of the allow-list.
        var anyUsersExist = await userRepository.AnyAsync(cancellationToken);
        if (!anyUsersExist)
        {
            var admin = await CreateUserWithPasswordAsync(
                email, displayName, password, UserRole.Administrator, isPrimaryAdministrator: true, cancellationToken);
            return UserProvisioningResult.Success(admin);
        }

        var isAllowListed = await allowedEmailRepository.IsAllowedAsync(email, cancellationToken);
        if (!isAllowListed)
        {
            return UserProvisioningResult.Denied("This email has not been added to the allowed list.");
        }

        var user = await CreateUserWithPasswordAsync(
            email, displayName, password, UserRole.User, isPrimaryAdministrator: false, cancellationToken);
        return UserProvisioningResult.Success(user);
    }

    public async Task<PasswordSignInResult> SignInWithPasswordAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        email = EmailNormalizer.Normalize(email);

        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.PasswordHash is null)
        {
            return PasswordSignInResult.InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return PasswordSignInResult.InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var rehashed = passwordHasher.HashPassword(user, password);
            await userRepository.UpdatePasswordHashAsync(user.Id, rehashed, cancellationToken);
        }

        await userRepository.UpdateLastLoginAsync(user.Id, DateTimeOffset.UtcNow, cancellationToken);
        return PasswordSignInResult.Success(user);
    }

    private async Task<User> CreateUserWithPasswordAsync(
        string email, string displayName, string password, UserRole role, bool isPrimaryAdministrator, CancellationToken cancellationToken)
    {
        var user = NewUser(email, displayName, googleSubjectId: null, role, isPrimaryAdministrator);
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        return await userRepository.AddAsync(user, cancellationToken);
    }

    private async Task<User> CreateGoogleUserAsync(
        string email, string displayName, string googleSubjectId,
        UserRole role, bool isPrimaryAdministrator, CancellationToken cancellationToken)
    {
        var user = NewUser(email, displayName, googleSubjectId, role, isPrimaryAdministrator);

        return await userRepository.AddAsync(user, cancellationToken);
    }

    private static User NewUser(string email, string displayName, string? googleSubjectId, UserRole role, bool isPrimaryAdministrator)
    {
        var now = DateTimeOffset.UtcNow;

        return new User
        {
            Id = Guid.NewGuid(),
            GoogleSubjectId = googleSubjectId,
            Email = EmailNormalizer.Normalize(email),
            DisplayName = displayName,
            Role = role,
            IsPrimaryAdministrator = isPrimaryAdministrator,
            CreatedAt = now,
            LastLoginAt = now
        };
    }
}
