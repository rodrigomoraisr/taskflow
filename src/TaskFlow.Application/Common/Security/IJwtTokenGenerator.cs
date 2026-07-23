using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(
        User user,
        Guid workspaceId,
        WorkspaceRole role);
}