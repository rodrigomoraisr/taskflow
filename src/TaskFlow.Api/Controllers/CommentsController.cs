using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Comments;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId:guid}/tasks/{taskId:guid}/comments")]
public sealed class CommentsController(ICommentService comments) : ControllerBase
{
    [EndpointSummary("Comment on a task")]
    [EndpointDescription("""
        Member, Admin or Owner. Body is trimmed, required, and limited to 2000 characters. The task and project
        must be active. Bearer authentication is required. Non-members receive the same 404 as an unknown
        workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/comments
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"body":"The endpoint examples are ready for review."}
        ```

        Success: HTTP 201.
        """)]
    [ProducesResponseType(typeof(CommentResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"body\":\"The endpoint examples are ready for review.\"}")]
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Create(Guid workspaceId, Guid taskId,
        WriteCommentRequest request, CancellationToken cancellationToken)
    {
        var response = await comments.CreateAsync(workspaceId, taskId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, taskId, id = response.Id }, response);
    }

    [EndpointSummary("Get a comment")]
    [EndpointDescription("""
        All active workspace roles. Deleted comments and comments beneath deleted tasks/projects are absent.
        Bearer authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/comments/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(CommentResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CommentResponse>> GetById(Guid workspaceId, Guid taskId,
        Guid id, CancellationToken cancellationToken) =>
        Ok(await comments.GetByIdAsync(workspaceId, taskId, id, cancellationToken));

    [EndpointSummary("List task comments")]
    [EndpointDescription("""
        All active workspace roles. Page starts at 1; pageSize defaults to 20 and is capped at 100. Bearer
        authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/comments?page=1&pageSize=20
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(List<CommentResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet]
    public async Task<ActionResult<List<CommentResponse>>> GetPaged(Guid workspaceId, Guid taskId,
        [FromQuery] GetCommentsRequest request, CancellationToken cancellationToken) =>
        Ok(await comments.GetPagedAsync(workspaceId, taskId, request, cancellationToken));

    [EndpointSummary("Edit my comment")]
    [EndpointDescription("""
        Member, Admin or Owner, and the caller must be the author. Admin/Owner does not override authorship. Body
        is trimmed and limited to 2000 characters. Edits have no concurrency token. Bearer authentication is
        required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        PUT /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/comments/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"body":"The examples have been reviewed."}
        ```

        Success: HTTP 204 (empty body).
        """)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"body\":\"The examples have been reviewed.\"}")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid workspaceId, Guid taskId, Guid id,
        WriteCommentRequest request, CancellationToken cancellationToken)
    {
        await comments.EditAsync(workspaceId, taskId, id, request, cancellationToken);
        return NoContent();
    }

    [EndpointSummary("Soft-delete my comment")]
    [EndpointDescription("""
        Member, Admin or Owner, and author only. Records comment action metadata without copying the body into
        activity history. Bearer authentication is required. Non-members receive the same 404 as an unknown
        workspace.

        Example request (replace sample IDs with your own):

        ```http
        DELETE /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/comments/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 204 (empty body).
        """)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid taskId, Guid id,
        CancellationToken cancellationToken)
    {
        await comments.DeleteAsync(workspaceId, taskId, id, cancellationToken);
        return NoContent();
    }
}
