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
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Create(Guid workspaceId, Guid taskId,
        WriteCommentRequest request, CancellationToken cancellationToken)
    {
        var response = await comments.CreateAsync(workspaceId, taskId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, taskId, id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CommentResponse>> GetById(Guid workspaceId, Guid taskId,
        Guid id, CancellationToken cancellationToken) =>
        Ok(await comments.GetByIdAsync(workspaceId, taskId, id, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<List<CommentResponse>>> GetPaged(Guid workspaceId, Guid taskId,
        [FromQuery] GetCommentsRequest request, CancellationToken cancellationToken) =>
        Ok(await comments.GetPagedAsync(workspaceId, taskId, request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid workspaceId, Guid taskId, Guid id,
        WriteCommentRequest request, CancellationToken cancellationToken)
    {
        await comments.EditAsync(workspaceId, taskId, id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid taskId, Guid id,
        CancellationToken cancellationToken)
    {
        await comments.DeleteAsync(workspaceId, taskId, id, cancellationToken);
        return NoContent();
    }
}
