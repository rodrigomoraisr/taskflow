using Microsoft.AspNetCore.Http;
using TaskFlow.Api.Middleware;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Api.IntegrationTests.Middleware;

/// <summary>
/// Pins the 403-versus-404 decision, which is the contract the whole
/// tenant-isolation suite asserts against:
///
///   - not a member of the workspace  -> 404, identical to a workspace that
///     does not exist, because confirming existence across a tenant boundary
///     is itself a leak;
///   - a member whose role is too low -> 403, since that caller already knows
///     the workspace exists and learns nothing new.
/// </summary>
public class ExceptionMiddlewareTests
{
    private static async Task<(int StatusCode, string Body)> InvokeAsync(
        Exception exception)
    {
        var middleware = new ExceptionMiddleware(
            _ => throw exception);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvokeAsync_WhenCallerIsNotAWorkspaceMember_ShouldReturn404()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var (statusCode, _) = await InvokeAsync(
            new UnauthorizedWorkspaceAccessException(workspaceId));

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenWorkspaceDoesNotExist_ShouldReturn404()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var (statusCode, _) = await InvokeAsync(
            new WorkspaceNotFoundException(workspaceId));

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    /// <summary>
    /// The heart of the decision: an outsider probing for which workspaces
    /// exist must get back exactly the same bytes either way.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenCallerIsNotAMember_ShouldBeIndistinguishableFromAMissingWorkspace()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var outsider = await InvokeAsync(
            new UnauthorizedWorkspaceAccessException(workspaceId));

        var missing = await InvokeAsync(
            new WorkspaceNotFoundException(workspaceId));

        // Assert
        Assert.Equal(missing.StatusCode, outsider.StatusCode);
        Assert.Equal(missing.Body, outsider.Body);
    }

    [Fact]
    public async Task InvokeAsync_WhenCallerIsNotAMember_ShouldNotRevealThatTheyAreNotAMember()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var (_, body) = await InvokeAsync(
            new UnauthorizedWorkspaceAccessException(workspaceId));

        // Assert — the exception's own message is for the application layer and
        // must not reach the caller.
        Assert.DoesNotContain("does not belong", body);
        Assert.DoesNotContain("permission", body);
    }

    [Fact]
    public async Task InvokeAsync_WhenCallerIsAMemberWithInsufficientRole_ShouldReturn403()
    {
        // Act
        var (statusCode, _) = await InvokeAsync(
            new InsufficientWorkspaceRoleException());

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenTaskAssignmentIsNotPermittedForTheRole_ShouldReturn403()
    {
        // Act
        var (statusCode, _) = await InvokeAsync(
            new InvalidTaskAssignmentException());

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    /// <summary>
    /// All four "already deleted" domain exceptions must be 409, not 500.
    ///
    /// Only the project one is reachable through the API today: every
    /// repository filters soft-deleted rows before an entity guard can fire, so
    /// the service raises a not-found first — SoftDeleteVisibilityTests proves
    /// that by getting 404 for every deleted-entity operation. The other three
    /// are unreachable from HTTP, which makes this the only place they can be
    /// tested, and the reason the test lives here rather than in the isolation
    /// suite. Without the mapping, the first repository method written without
    /// that filter would turn a domain rule into a 500.
    /// </summary>
    [Theory]
    [MemberData(nameof(AlreadyDeletedExceptions))]
    public async Task InvokeAsync_WhenAnEntityIsAlreadyDeleted_ShouldReturn409(
        Exception exception)
    {
        // Act
        var (statusCode, _) = await InvokeAsync(exception);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    public static TheoryData<Exception> AlreadyDeletedExceptions() =>
    [
        new ProjectAlreadyDeletedException(Guid.NewGuid()),
        new TaskAlreadyDeletedException(Guid.NewGuid()),
        new WorkspaceAlreadyDeletedException(Guid.NewGuid()),
        new WorkspaceUserAlreadyDeletedException(Guid.NewGuid())
    ];

    [Fact]
    public async Task InvokeAsync_WhenTheExceptionIsUnmapped_ShouldReturn500WithoutTheDetail()
    {
        // Act
        var (statusCode, body) = await InvokeAsync(
            new InvalidOperationException("connection string user=admin"));

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.DoesNotContain("connection string", body);
    }
}
