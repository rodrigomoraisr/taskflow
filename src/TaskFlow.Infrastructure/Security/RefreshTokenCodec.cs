using System.Security.Cryptography;
using System.Text;
using TaskFlow.Application.Common.Security;

namespace TaskFlow.Infrastructure.Security;

public sealed class RefreshTokenCodec : IRefreshTokenCodec
{
    public string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
