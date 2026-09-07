using TaskFlow.Application.Users;

namespace TaskFlow.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default);
}
