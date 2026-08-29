using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                TaskNotFoundException =>
                    (StatusCodes.Status404NotFound, ex.Message),

                UserAlreadyExistsException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                InvalidCredentialsException =>
                    (StatusCodes.Status401Unauthorized, ex.Message),

                UserWithoutWorkspaceException =>
                    (StatusCodes.Status403Forbidden, ex.Message),
                    
                InvalidUserIdentityException =>
                    (StatusCodes.Status401Unauthorized, ex.Message),

                InvalidWorkspaceIdentityException =>
                    (StatusCodes.Status401Unauthorized, ex.Message),

                InsufficientWorkspaceRoleException =>
                    (StatusCodes.Status403Forbidden, ex.Message),

                UnauthorizedWorkspaceAccessException =>
                    (StatusCodes.Status403Forbidden, ex.Message),
                    
                WorkspaceNotFoundException =>
                    (StatusCodes.Status404NotFound, ex.Message),

                UserNotFoundException =>
                    (StatusCodes.Status404NotFound, ex.Message),

                WorkspaceMemberNotFoundException =>
                    (StatusCodes.Status404NotFound, ex.Message),

                WorkspaceMemberAlreadyExistsException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                LastWorkspaceOwnerException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                InvalidWorkspaceRoleException =>
                    (StatusCodes.Status400BadRequest, ex.Message),

                ProjectNotFoundException =>
                    (StatusCodes.Status404NotFound, ex.Message),

                ProjectAlreadyDeletedException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                ArgumentException =>
                    (StatusCodes.Status400BadRequest, ex.Message),

                _ =>
                    (
                        StatusCodes.Status500InternalServerError,
                        "An unexpected error occurred."
                    )
            };

            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = message
                });
        }
    }
}
