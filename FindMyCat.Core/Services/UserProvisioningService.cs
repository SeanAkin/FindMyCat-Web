using FindMyCat.Core.Entities;
using FindMyCat.Core.Errors;
using FindMyCat.Core.Models;
using FindMyCat.Core.RepositoryContracts;
using FindMyCat.Core.Security;
using Microsoft.AspNetCore.Identity;

namespace FindMyCat.Core.Services;

public interface IUserProvisioningService
{
    Task<User> ProvisionOrSignInAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken = default);

    Task<User> RegisterWithPasswordAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="InvalidCredentialsException"/> for an unknown email and a wrong password
    /// alike, so a caller cannot learn which of the two it got wrong.
    /// </summary>
    Task<User> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken = default);
}

public sealed class UserProvisioningService(
    IUserRepository userRepository,
    IAllowedEmailRepository allowedEmailRepository,
    IPasswordHasher<User> passwordHasher) : IUserProvisioningService
{
    public async Task<User> ProvisionOrSignInAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken = default)
    {
        var email = EmailNormalizer.Normalize(googleUser.Email);

        var existing = await userRepository.GetByGoogleSubjectIdAsync(googleUser.GoogleSubjectId, cancellationToken);
        if (existing is not null)
        {
            await userRepository.UpdateLastLoginAsync(existing.Id, DateTimeOffset.UtcNow, cancellationToken);
            return existing;
        }

        var emailOwner = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (emailOwner is not null)
        {
            throw new EmailRegisteredWithPasswordException();
        }

        // First user = Primary Admin
        var anyUsersExist = await userRepository.AnyAsync(cancellationToken);
        if (!anyUsersExist)
        {
            return await CreateGoogleUserAsync(
                email, googleUser.DisplayName, googleUser.GoogleSubjectId,
                UserRole.Administrator, isPrimaryAdministrator: true, cancellationToken);
        }

        await RequireAllowListedAsync(email, cancellationToken);

        return await CreateGoogleUserAsync(
            email, googleUser.DisplayName, googleUser.GoogleSubjectId,
            UserRole.User, isPrimaryAdministrator: false, cancellationToken);
    }

    public async Task<User> RegisterWithPasswordAsync(
        string email, string displayName, string password, CancellationToken cancellationToken = default)
    {
        email = EmailNormalizer.Normalize(email);

        var passwordViolations = PasswordPolicy.GetViolations(password);
        if (passwordViolations.Count > 0)
        {
            throw new WeakPasswordException(passwordViolations);
        }

        var emailOwner = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (emailOwner is not null)
        {
            throw new EmailAlreadyRegisteredException();
        }

        // First user = Primary Admin, regardless of the allow-list.
        var anyUsersExist = await userRepository.AnyAsync(cancellationToken);
        if (!anyUsersExist)
        {
            return await CreateUserWithPasswordAsync(
                email, displayName, password, UserRole.Administrator, isPrimaryAdministrator: true, cancellationToken);
        }

        await RequireAllowListedAsync(email, cancellationToken);

        return await CreateUserWithPasswordAsync(
            email, displayName, password, UserRole.User, isPrimaryAdministrator: false, cancellationToken);
    }

    public async Task<User> SignInWithPasswordAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        email = EmailNormalizer.Normalize(email);

        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.PasswordHash is null)
        {
            throw new InvalidCredentialsException();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var rehashed = passwordHasher.HashPassword(user, password);
            await userRepository.UpdatePasswordHashAsync(user.Id, rehashed, cancellationToken);
        }

        await userRepository.UpdateLastLoginAsync(user.Id, DateTimeOffset.UtcNow, cancellationToken);
        return user;
    }

    private async Task RequireAllowListedAsync(string email, CancellationToken cancellationToken)
    {
        var isAllowListed = await allowedEmailRepository.IsAllowedAsync(email, cancellationToken);
        if (!isAllowListed)
        {
            throw new NotAllowListedException();
        }
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
