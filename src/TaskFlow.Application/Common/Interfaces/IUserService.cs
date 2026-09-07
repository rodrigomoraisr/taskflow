using TaskFlow.Application.Users;

public interface IUserService
{
    Task <RegisterResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

}
