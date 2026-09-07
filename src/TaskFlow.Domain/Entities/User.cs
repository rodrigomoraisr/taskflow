using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class User : BaseEntity
{

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndsAt { get; private set; }

    public bool IsLockedAt(DateTime now) => LockoutEndsAt.HasValue && now < LockoutEndsAt.Value;

    public void RecordFailedLogin(DateTime now)
    {
        if (IsLockedAt(now)) return;
        if (LockoutEndsAt.HasValue) ResetFailedLogins();
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5) LockoutEndsAt = now.AddMinutes(15);
    }

    public void ResetFailedLogins()
    {
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
    }

    public User(
    string email,
    string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "Email is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException(
                "Password hash is required.");

        Id = Guid.NewGuid();
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }
}