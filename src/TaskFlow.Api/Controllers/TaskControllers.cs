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

    [EndpointSummary("Create a task")]
    [EndpointDescription("""
        Member, Admin or Owner. The project must be active and in this workspace. Priority: Low=1, Medium=2,
        High=3, Critical=4. Bearer authentication is required. Non-members receive the same 404 as an unknown
        workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/tasks
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"title":"Document the API","description":"Add endpoint examples","projectId":"22222222-2222-2222-2222-222222222222","priority":2,"dueDate":"2026-12-01T12:00:00Z"}
        ```

        Success: HTTP 201.
        """)]
    [ProducesResponseType(typeof(CreateTaskResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    [TaskFlow.Api.OpenApi.RequestExample("{\"title\":\"Document the API\",\"description\":\"Add endpoint examples\",\"projectId\":\"22222222-2222-2222-2222-222222222222\",\"priority\":2,\"dueDate\":\"2026-12-01T12:00:00Z\"}")]
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

    [EndpointSummary("Get a task")]
    [EndpointDescription("""
        All active workspace roles. Tasks beneath deleted projects are also absent. Bearer authentication is
        required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(GetTaskResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
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

    [EndpointSummary("Filter and sort tasks")]
    [EndpointDescription("""
        All active workspace roles. Page defaults to 1; pageSize defaults to 20 (maximum 100). Status: Todo=1,
        InProgress=2, Done=3. Priority: Low=1, Medium=2, High=3, Critical=4. Combine filters with AND. Unassigned
        cannot be combined with assigneeUserId. Due-date bounds are inclusive and normalized to UTC. Sort by
        createdAt (default), updatedAt, title, priority, status or dueDate; direction desc (default) or asc. Null
        dates are last and task ID breaks ties. Bearer authentication is required. Non-members receive the same
        404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        GET /api/workspaces/11111111-1111-1111-1111-111111111111/tasks?page=1&pageSize=20&status=1&sortBy=dueDate&sortDirection=asc
        Authorization: Bearer <access-token>
        ```

        Success: HTTP 200.
        """)]
    [ProducesResponseType(typeof(GetTasksResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
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

    [EndpointSummary("Update task details")]
    [EndpointDescription("""
        Member, Admin or Owner. Priority: Low=1, Medium=2, High=3, Critical=4. Overlapping writes may return 409;
        no client version precondition is supported. Bearer authentication is required. Non-members receive the
        same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        PUT /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"title":"Document the API","description":"Review examples","priority":3,"dueDate":null}
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
    [TaskFlow.Api.OpenApi.RequestExample("{\"title\":\"Document the API\",\"description\":\"Review examples\",\"priority\":3,\"dueDate\":null}")]
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

    [EndpointSummary("Soft-delete a task")]
    [EndpointDescription("""
        Member, Admin or Owner. Records activity in the same transaction; later normal reads return 404. Bearer
        authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        DELETE /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111
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

    [EndpointSummary("Start a task")]
    [EndpointDescription("""
        Member, Admin or Owner. Transitions Todo to InProgress; invalid transitions and overlapping writes return
        409. Bearer authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/start
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
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await _taskService.StartAsync(
            workspaceId,
            id,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("Complete a task")]
    [EndpointDescription("""
        Member, Admin or Owner. Transitions Todo or InProgress to Done; invalid transitions and overlapping
        writes return 409. Bearer authentication is required. Non-members receive the same 404 as an unknown
        workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/complete
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
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await _taskService.CompleteAsync(
            workspaceId,
            id,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("Reopen a task")]
    [EndpointDescription("""
        Member, Admin or Owner. Transitions Done to Todo; invalid transitions and overlapping writes return 409.
        Bearer authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        POST /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/reopen
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
    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await _taskService.ReopenAsync(
            workspaceId,
            id,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("Assign a task")]
    [EndpointDescription("""
        Owner/Admin may assign an active workspace member. A Member may only claim an unassigned task for
        themselves. Viewers cannot assign. Bearer authentication is required. Non-members receive the same 404 as
        an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        PUT /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/assignee
        Authorization: Bearer <access-token>
        Content-Type: application/json

        {"userId":"33333333-3333-3333-3333-333333333333"}
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
    [TaskFlow.Api.OpenApi.RequestExample("{\"userId\":\"33333333-3333-3333-3333-333333333333\"}")]
    [HttpPut("{id:guid}/assignee")]
    public async Task<IActionResult> Assign(
        Guid workspaceId,
        Guid id,
        AssignTaskRequest request,
        CancellationToken cancellationToken)
    {
        await _taskService.AssignAsync(
            workspaceId,
            id,
            request.UserId,
            cancellationToken);

        return NoContent();
    }

    [EndpointSummary("Unassign a task")]
    [EndpointDescription("""
        Owner or Admin only. Removes the current assignee and records a change when applicable. Bearer
        authentication is required. Non-members receive the same 404 as an unknown workspace.

        Example request (replace sample IDs with your own):

        ```http
        DELETE /api/workspaces/11111111-1111-1111-1111-111111111111/tasks/11111111-1111-1111-1111-111111111111/assignee
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
    [HttpDelete("{id:guid}/assignee")]
    public async Task<IActionResult> Unassign(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await _taskService.UnassignAsync(
            workspaceId,
            id,
            cancellationToken);

        return NoContent();
    }

}
