using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Activity;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId:guid}/activity")]
public sealed class ActivityController(IActivityService activity) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ActivityResponse>>> GetPaged(Guid workspaceId,
        [FromQuery] GetActivityRequest request, CancellationToken cancellationToken) =>
        Ok(await activity.GetPagedAsync(workspaceId, request, cancellationToken));
}
