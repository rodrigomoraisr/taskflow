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

    [HttpGet("{id}")]
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
    Guid id,
    CancellationToken cancellationToken)
    {
        await _workspaceService.DeleteAsync(
            id,
            cancellationToken);

        return NoContent();
    }
}