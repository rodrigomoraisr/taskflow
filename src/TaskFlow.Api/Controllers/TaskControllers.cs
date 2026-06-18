using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Tasks;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("tasks")]
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
    public async Task<ActionResult<CreateTaskResponse>> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var response = await _taskService.CreateAsync(request, cancellationToken);
        return Created($"/tasks/{response.Id}", response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetTaskResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var response = await _taskService.GetByIdAsync(
            id,
            cancellationToken);

        return Ok(response);
    }
}