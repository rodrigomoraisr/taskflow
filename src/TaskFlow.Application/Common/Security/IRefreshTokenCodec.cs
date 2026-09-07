namespace TaskFlow.Application.Common.Security;

public interface IRefreshTokenCodec
{
    string Generate();
    string Hash(string token);
}
