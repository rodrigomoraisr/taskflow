using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Users;

namespace TaskFlow.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthenticationService _authentication;

    public AuthController(
        IUserService userService, IAuthenticationService authentication)
    {
        _userService = userService;
        _authentication = authentication;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _userService.RegisterAsync(
            request,
            cancellationToken);

        return Created(
            $"/users/{response.Id}",
            response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authentication.LoginAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
        => Ok(await _authentication.RefreshAsync(request, cancellationToken));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authentication.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
