using TaskFlow.Domain.Entities;

namespace TaskFlow.TestSupport.Builders;

/// <summary>
/// Builds <see cref="User"/> instances for tests.
///
/// The default email is unique per instance because the users table has a
/// unique index on it: a fixed default would make any test that seeds two users
/// fail on a constraint violation rather than on the behaviour it was written
/// to check.
/// </summary>
public sealed class UserBuilder
{
    private string _email = $"user-{Guid.NewGuid():N}@taskflow.test";
    private string _passwordHash = "not-a-real-bcrypt-hash";

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithPasswordHash(string passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }

    public User Build()
    {
        return new User(
            _email,
            _passwordHash);
    }
}
