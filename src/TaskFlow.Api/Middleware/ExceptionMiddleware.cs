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

                InvalidUserIdentityException =>
                    (StatusCodes.Status401Unauthorized, ex.Message),

                InvalidWorkspaceIdentityException =>
                    (StatusCodes.Status401Unauthorized, ex.Message),

                InsufficientWorkspaceRoleException =>
                    (StatusCodes.Status403Forbidden, ex.Message),

                // A non-member must not be able to tell a workspace they are
                // locked out of from one that does not exist, so this leaves as
                // the same 404, with the same body, as WorkspaceNotFound below.
                // Insufficient role stays 403: that caller is a member, and
                // telling them their role is too low leaks nothing.
                UnauthorizedWorkspaceAccessException unauthorized =>
                    (
                        StatusCodes.Status404NotFound,
                        WorkspaceNotFound(unauthorized.WorkspaceId)
                    ),
                    
                WorkspaceNotFoundException notFound =>
                    (
                        StatusCodes.Status404NotFound,
                        WorkspaceNotFound(notFound.WorkspaceId)
                    ),

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

                InvalidTaskStatusTransitionException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                InvalidTaskAssignmentException =>
                    (StatusCodes.Status403Forbidden, ex.Message),

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

    /// <summary>
    /// The single place that decides what a caller is told about a workspace
    /// they cannot see. Both "not a member" and "no such workspace" render
    /// through here, so the two responses are byte-identical and an outsider
    /// cannot probe for which workspaces exist.
    /// </summary>
    private static string WorkspaceNotFound(Guid workspaceId)
    {
        return $"The workspace {workspaceId} was not found.";
    }
}
