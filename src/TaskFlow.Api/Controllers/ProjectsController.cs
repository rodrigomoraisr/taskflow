using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Projects;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId:guid}/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [EndpointSummary("Create a project")]
    [EndpointDescription("""
        Owner or Admin only. Name is required (maximum 200 characters); description allows up to 2000. Bearer
        authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/projects
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"name":"API launch","description":"Prepare the first release."}
        ```

        Success: HTTP 201.
        """)]
    [ProducesResponseType(typeof(CreateProjectResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"name\":\"API launch\",\"description\":\"Prepare the first release.\"}")]
    [HttpPost]
    public async Task<ActionResult<CreateProjectResponse>> Create(
        Guid workspaceId,
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _projectService.CreateAsync(
            workspaceId,
            request,
            cancellationToken);

        return Created(
            $"/api/workspaces/{workspaceId}/projects/{response.Id}",
            response);
    }

    [EndpointSummary("List projects")]
    [EndpointDescription("""
        All active workspace roles can list active projects. Bearer authentication is required. Non-members
        receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/projects
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(GetProjectsResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet]
    public async Task<ActionResult<GetProjectsResponse>> GetProjects(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var response = await _projectService.GetProjectsAsync(
            workspaceId,
            cancellationToken);

        return Ok(response);
    }

    [EndpointSummary("Get a project")]
    [EndpointDescription("""
        All active workspace roles can read the project. Soft-deleted projects are absent. Bearer authentication
        is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/projects/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(GetProjectResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<GetProjectResponse>> GetById(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var response = await _projectService.GetByIdAsync(
            workspaceId,
            projectId,
            cancellationToken);

        return Ok(response);
    }

    [EndpointSummary("Update a project")]
    [EndpointDescription("""
        Owner or Admin only. Status: Active=1, OnHold=2, Completed=3. Overlapping writes may return 409; there is
        no If-Match precondition. Bearer authentication is required. Non-members receive the same 404 as an
        unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        PUT /api/workspaces/11111111-1111-1111-1111-111111111111/projects/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"name":"API launch","description":"Release preparation","status":1}
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
    [TaskFlow.Api.OpenApi.RequestExample("{\"name\":\"API launch\",\"description\":\"Release preparation\",\"status\":1}")]
    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> Update(
        Guid workspaceId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await _projectService.UpdateAsync(
            workspaceId,
            projectId,
            request,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("Soft-delete a project")]
    [EndpointDescription("""
        Owner or Admin only. Also hides its tasks from reads without deleting their rows. Overlapping writes may
        return 409. Bearer authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        DELETE /api/workspaces/11111111-1111-1111-1111-111111111111/projects/11111111-1111-1111-1111-111111111111
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
    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await _projectService.DeleteAsync(
            workspaceId,
            projectId,
            cancellationToken);

        return NoContent();
    }
}
