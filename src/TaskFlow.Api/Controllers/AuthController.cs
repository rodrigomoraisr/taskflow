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

    [EndpointSummary("Register an account")]
    [EndpointDescription("""
        Creates a user and a default workspace owned by that user; log in separately to obtain tokens. Passwords
        require at least 15 Unicode scalar values and at most 72 UTF-8 bytes, without control characters. The
        Location header identifies the user but there is no GET /users endpoint. Auth responses use
        Cache-Control: no-store.

        Example request (replace sample IDs with your own):

        ```http
        POST /auth/register
        Content-Type: application/json

        {"email":"developer@example.com","password":"An example passphrase 42!"}
        ```

        Success: HTTP 201.
        """)]
    [ProducesResponseType(typeof(RegisterResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"email\":\"developer@example.com\",\"password\":\"An example passphrase 42!\"}")]
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

    [EndpointSummary("Start an authentication session")]
    [EndpointDescription("""
        Returns a short-lived access JWT and rotating refresh token. Invalid credentials and locked accounts
        share the same 401 response. Five failed attempts lock the account for 15 minutes. Auth responses use
        Cache-Control: no-store.

        Example request (replace sample IDs with your own):

        ```http
        POST /auth/login
        Content-Type: application/json

        {"email":"developer@example.com","password":"An example passphrase 42!"}
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"email\":\"developer@example.com\",\"password\":\"An example passphrase 42!\"}")]
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
    [EndpointSummary("Rotate a refresh token")]
    [EndpointDescription("""
        Single-use rotation; replace the stored refresh token after success and serialize refresh requests.
        Reusing an old token revokes its session. Sessions expire after seven days. Auth responses use
        Cache-Control: no-store.

        Example request (replace sample IDs with your own):

        ```http
        POST /auth/refresh
        Content-Type: application/json

        {"refreshToken":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"refreshToken\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
        => Ok(await _authentication.RefreshAsync(request, cancellationToken));

    [EndpointSummary("Revoke a refresh session")]
    [EndpointDescription("""
        Revokes the session associated with this refresh token. Existing access JWTs remain usable until
        expiration. Well-formed unknown or already-revoked tokens also return 204. Auth responses use
        Cache-Control: no-store.

        Example request (replace sample IDs with your own):

        ```http
        POST /auth/logout
        Content-Type: application/json

        {"refreshToken":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}
        ```

        Success: HTTP 204 (empty body).
        """)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"refreshToken\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authentication.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
