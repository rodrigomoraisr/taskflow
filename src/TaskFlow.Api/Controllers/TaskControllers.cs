using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Tasks;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(
        ITaskService taskService
    )
    {
        _taskService = taskService;
    }

    [HttpPost]
    public async Task<ActionResult<CreateTaskResponse>> Create(
        Guid workspaceId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskService.CreateAsync(
            workspaceId,
            request,
            cancellationToken);

        return Created(
            $"/api/workspaces/{workspaceId}/tasks/{response.Id}",
            response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetTaskResponse>> Get(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _taskService.GetByIdAsync(
            workspaceId,
            id,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<GetTasksResponse>> GetTasks(
        Guid workspaceId,
        [FromQuery] GetTasksRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskService.GetTasksAsync(
            workspaceId,
            request,
            cancellationToken
        );

        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid workspaceId,
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        await _taskService.UpdateAsync(
            workspaceId,
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await _taskService.DeleteAsync(
            workspaceId,
            id,
            cancellationToken);

        return NoContent();
    }

}
