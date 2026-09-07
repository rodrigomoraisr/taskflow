using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller disconnected. There is no response to write and no server failure.
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Request failed after the response started. CorrelationId: {CorrelationId}",
                    context.TraceIdentifier);
                throw;
            }
            var (statusCode, message) = ex switch
            {
                ConcurrencyConflictException => (StatusCodes.Status409Conflict, ex.Message),
                CommentNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                CommentOwnershipException => (StatusCodes.Status403Forbidden, ex.Message),
                CommentAlreadyDeletedException => (StatusCodes.Status409Conflict, ex.Message),

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

                // Repositories normally hide deleted entities and yield 404.
                // Keep the domain guards mapped for paths that do reach them.
                ProjectAlreadyDeletedException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                TaskAlreadyDeletedException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                WorkspaceAlreadyDeletedException =>
                    (StatusCodes.Status409Conflict, ex.Message),

                WorkspaceUserAlreadyDeletedException =>
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

            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled request failure. CorrelationId: {CorrelationId}",
                    context.TraceIdentifier);
            else
                _logger.LogInformation("Request rejected with {StatusCode} ({FailureType}). CorrelationId: {CorrelationId}",
                    statusCode, ex.GetType().Name, context.TraceIdentifier);

            await ApiProblems.WriteAsync(context, statusCode, message, context.RequestAborted);
        }
    }

    /// <summary>
    /// The single place that decides what a caller is told about a workspace
    /// they cannot see. Both "not a member" and "no such workspace" render
    /// through here, so their problem fields match (apart from request correlation) and an outsider
    /// cannot probe for which workspaces exist.
    /// </summary>
    private static string WorkspaceNotFound(Guid workspaceId)
    {
        return $"The workspace {workspaceId} was not found.";
    }
}
