using TaskFlow.Api.Security;

namespace TaskFlow.Api.IntegrationTests.Security;

public class JwtSigningKeyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("short-key")]
    [InlineData("@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/jwt)")]
    [InlineData("  @microsoft.keyvault(VaultName=example;SecretName=jwt)")]
    public void Validate_WhenKeyIsUnsafe_ShouldRejectWithoutDisclosingValue(string? key)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => JwtSigningKey.Validate(key));

        if (!string.IsNullOrWhiteSpace(key))
            Assert.DoesNotContain(key, exception.Message);
    }

    [Fact]
    public void Validate_WhenKeyHasEnoughBytes_ShouldPreserveItsValue()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

        Assert.Equal(key, JwtSigningKey.Validate(key));
    }
}
