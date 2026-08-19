using System.Security.Claims;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Api.Security;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(
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
}