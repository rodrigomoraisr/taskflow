using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Activity;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId:guid}/activity")]
public sealed class ActivityController(IActivityService activity) : ControllerBase
{
    [EndpointSummary("Read workspace activity")]
    [EndpointDescription("""
        All active workspace roles. Filter optionally by taskId. Page starts at 1; pageSize defaults to 20 and is
        capped at 100. History includes deleted tasks/projects; comment actions contain IDs, never comment
        bodies. Bearer authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/activity?page=1&pageSize=20
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(List<ActivityResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet]
    public async Task<ActionResult<List<ActivityResponse>>> GetPaged(Guid workspaceId,
        [FromQuery] GetActivityRequest request, CancellationToken cancellationToken) =>
        Ok(await activity.GetPagedAsync(workspaceId, request, cancellationToken));
}
