using System.Text;

namespace TaskFlow.Api.Security;

public static class JwtSigningKey
{
    public static string Validate(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "Jwt:Key is not configured. In development set it with "
                + "dotnet user-secrets set \"Jwt:Key\" \"<random value>\"; "
                + "elsewhere supply the Jwt__Key environment variable.");

        // App Service returns the reference literally when secret resolution fails.
        // Its length must never allow that public reference to become a signing key.
        if (key.TrimStart().StartsWith("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Jwt:Key contains an unresolved Key Vault reference. Check the app's identity and secret access.");

        if (Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException(
                "Jwt:Key is too short. HMAC-SHA256 signing requires at least 32 bytes (256 bits).");

        return key;
    }
}
