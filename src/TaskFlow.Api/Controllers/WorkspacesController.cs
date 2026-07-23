using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Workspaces;

[ApiController]
[Route("api/workspaces")]
[Authorize]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspacesController(
        IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
    CreateWorkspaceRequest request,
    CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(
            ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(
            userIdClaim.Value,
            out var userId))
        {
            return Unauthorized();
        }

        var response =
            await _workspaceService.CreateAsync(
                userId,
                request,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }
}