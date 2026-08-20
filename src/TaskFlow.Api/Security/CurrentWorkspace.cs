using System.Security.Claims;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Security;

public class CurrentWorkspace : ICurrentWorkspace
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentWorkspace(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var userIdClaim =
                _httpContextAccessor.HttpContext?
                    .User
                    .FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim is null)
                throw new InvalidUserIdentityException();

            if (!Guid.TryParse(
                    userIdClaim.Value,
                    out var userId))
            {
                throw new InvalidUserIdentityException();
            }

            return userId;
        }
    }

    public Guid WorkspaceId
    {
        get
        {
            var workSpaceIdClaim =
                _httpContextAccessor.HttpContext?
                .User
                .FindFirst("workspaceId");

            if (workSpaceIdClaim is null)
                throw new InvalidWorkspaceIdentityException();

            if (!Guid.TryParse(
                    workSpaceIdClaim.Value,
                    out var workspaceId))
            {
                throw new InvalidWorkspaceIdentityException();
            }

            return workspaceId;
        }
    }

    public WorkspaceRole Role{
        get
        {
            var workspaceRoleClaim =
                _httpContextAccessor.HttpContext?
                .User
                .FindFirst(ClaimTypes.Role);
            
            if(workspaceRoleClaim is null)
                throw new InvalidWorkspaceIdentityException();
            
            if (!Enum.TryParse<WorkspaceRole>(
                    workspaceRoleClaim.Value,
                    true, //ignore case sensitive
                    out var role)
                    || !Enum.IsDefined(typeof(WorkspaceRole), role))
            {
                throw new InvalidWorkspaceIdentityException();
            }

            return role;
        }
    }
}