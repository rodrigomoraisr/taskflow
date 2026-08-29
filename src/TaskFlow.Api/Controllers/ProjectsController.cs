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
