using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Workspaces;

[ApiController]
[Route("api/workspaces")]
[Authorize]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ICurrentUser _currentUser;

    public WorkspacesController(
        IWorkspaceService workspaceService,
        ICurrentUser currentUser)
    {
        _workspaceService = workspaceService;
        _currentUser = currentUser;
    }

    [EndpointSummary("Create a workspace")]
    [EndpointDescription("""
        Creates a workspace with the authenticated caller as Owner. Bearer authentication is required.
        Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"name":"Portfolio workspace"}
        ```

        Success: HTTP 201.
        """)]
    [ProducesResponseType(typeof(CreateWorkspaceResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"name\":\"Portfolio workspace\"}")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var response =
            await _workspaceService.CreateAsync(
                _currentUser.UserId,
                request,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [EndpointSummary("List my workspaces")]
    [EndpointDescription("""
        Lists only active workspaces with active caller membership. Bearer authentication is required.
        Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(List<ListWorkspaceResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet]
    public async Task<ActionResult<List<ListWorkspaceResponse>>> GetForUser(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.GetForUserAsync(
            _currentUser.UserId,
            cancellationToken);

        return Ok(response);
    }

    [EndpointSummary("Get a workspace")]
    [EndpointDescription("""
        Available to every active workspace role. Bearer authentication is required. Non-members receive the same
        404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(GetWorkspaceResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetWorkspaceResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response =
            await _workspaceService.GetByIdAsync(
                id,
                cancellationToken);

        return Ok(response);
    }

    [EndpointSummary("Soft-delete a workspace")]
    [EndpointDescription("""
        Owner only. Deleted workspaces are hidden from normal reads. Bearer authentication is required.
        Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        DELETE /api/workspaces/11111111-1111-1111-1111-111111111111
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
    public async Task<IActionResult> Delete(
    Guid id,
    CancellationToken cancellationToken)
    {
        await _workspaceService.DeleteAsync(
            id,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("List workspace members")]
    [EndpointDescription("""
        Owner or Admin only. Lists active memberships. Bearer authentication is required. Non-members receive the
        same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/members
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(List<GetWorkspaceMemberResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult<List<GetWorkspaceMemberResponse>>> GetMembers(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.GetMembersAsync(
            workspaceId,
            cancellationToken);

        return Ok(response);
    }

    [EndpointSummary("Add a workspace member")]
    [EndpointDescription("""
        Owner or Admin only. The email must belong to an existing registered user. Adds or reactivates membership
        as Member. Bearer authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/members
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"email":"teammate@example.com"}
        ```

        Success: HTTP 201.
        """)]
    [ProducesResponseType(typeof(GetWorkspaceMemberResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"email\":\"teammate@example.com\"}")]
    [HttpPost("{workspaceId:guid}/members")]
    public async Task<ActionResult<GetWorkspaceMemberResponse>> AddMember(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.AddMemberAsync(
            workspaceId,
            request,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [EndpointSummary("Change a member role")]
    [EndpointDescription("""
        Owners can manage all roles. Admins can manage Member/Viewer targets and assign Admin/Member/Viewer. The
        last active Owner cannot be demoted. Roles: Owner=1, Admin=2, Member=3, Viewer=4. Bearer authentication
        is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        PATCH /api/workspaces/11111111-1111-1111-1111-111111111111/members/11111111-1111-1111-1111-111111111111/role
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"role":3}
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
    [TaskFlow.Api.OpenApi.RequestExample("{\"role\":3}")]
    [HttpPatch("{workspaceId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(
        Guid workspaceId,
        Guid userId,
        ChangeWorkspaceMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        await _workspaceService.ChangeMemberRoleAsync(
            workspaceId,
            userId,
            request,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("Remove a workspace member")]
    [EndpointDescription("""
        Soft-deletes membership. Owners may manage all roles; Admins may remove Member/Viewer targets. The last
        active Owner cannot be removed. Bearer authentication is required. Non-members receive the same 404 as an
        unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        DELETE /api/workspaces/11111111-1111-1111-1111-111111111111/members/11111111-1111-1111-1111-111111111111
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
    [HttpDelete("{workspaceId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _workspaceService.RemoveMemberAsync(
            workspaceId,
            userId,
            cancellationToken);

        return NoContent();
    }
}
