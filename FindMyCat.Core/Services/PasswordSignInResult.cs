using FindMyCat.Core.Entities;

namespace FindMyCat.Core.Services;

public sealed class PasswordSignInResult
{
    public User? User { get; }

    public bool IsSuccess => User is not null;

    private PasswordSignInResult(User? user)
    {
        User = user;
    }

    public static PasswordSignInResult Success(User user) => new(user);

    public static PasswordSignInResult InvalidCredentials() => new(user: null);
}
