using TaskFlow.Domain.Entities;

namespace TaskFlow.Domain.Tests.Entities;

/// <summary>
/// <see cref="User"/> has the smallest surface of any entity — a constructor
/// and two invariants — and, before 8.4, no tests. The id assertion is the one
/// that earns its place: identity is what every membership row keys on.
/// </summary>
public class UserTests
{
    [Fact]
    public void Constructor_WhenDataIsValid_ShouldCreateAnActiveUser()
    {
        // Arrange
        const string email = "person@taskflow.test";
        const string passwordHash = "a-bcrypt-hash";

        // Act
        var user = new User(email, passwordHash);

        // Assert
        Assert.Equal(email, user.Email);
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.NotEqual(default, user.CreatedAt);
    }

    /// <summary>
    /// Generated in the domain rather than by EF Core's key convention, for
    /// the same reason as <c>Workspace</c>: a user built in a test that never
    /// reaches the database still needs a distinct identity, or the membership
    /// rows keyed on it collapse into each other.
    /// </summary>
    [Fact]
    public void Constructor_WhenCalledTwice_ShouldGiveEachUserItsOwnId()
    {
        // Arrange + Act
        var first = new User("first@taskflow.test", "hash");
        var second = new User("second@taskflow.test", "hash");

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenEmailIsBlank_ShouldThrowArgumentException(
        string email)
    {
        Assert.Throws<ArgumentException>(() => new User(email, "hash"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenPasswordHashIsBlank_ShouldThrowArgumentException(
        string passwordHash)
    {
        Assert.Throws<ArgumentException>(
            () => new User("person@taskflow.test", passwordHash));
    }
}
