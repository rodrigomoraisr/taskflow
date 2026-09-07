using System.Text;

namespace TaskFlow.Application.Users;

public static class PasswordPolicy
{
    // Keep BCrypt's input bound explicit: never silently truncate a password.
    public static bool HasSafeEncoding(string? password) =>
        !string.IsNullOrWhiteSpace(password) && Encoding.UTF8.GetByteCount(password) <= 72
        && !password.Any(char.IsControl);

    public static bool IsValidNewPassword(string? password) =>
        HasSafeEncoding(password) && password!.EnumerateRunes().Count() >= 15;
}
