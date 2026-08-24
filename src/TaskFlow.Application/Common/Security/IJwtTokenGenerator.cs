using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
