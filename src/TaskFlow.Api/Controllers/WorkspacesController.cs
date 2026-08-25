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

    [HttpGet]
    public async Task<ActionResult<List<ListWorkspaceResponse>>> GetForUser(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.GetForUserAsync(
            _currentUser.UserId,
            cancellationToken);

        return Ok(response);
    }

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
