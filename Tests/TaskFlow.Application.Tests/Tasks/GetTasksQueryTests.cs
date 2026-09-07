using NSubstitute;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Tests.TestSupport;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Tasks;

public sealed class GetTasksQueryTests
{
    [Fact]
    public async Task GetTasksAsync_WhenFiltered_ShouldPassSameQueryAndCancellationToPageAndCount()
    {
        var c = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);
        var query = new GetTasksRequest
        {
            Status = TaskItemStatus.InProgress, Priority = TaskPriority.High,
            AssigneeUserId = c.CallerId, ProjectId = Guid.NewGuid(),
            DueDateFrom = DateTimeOffset.UtcNow, SortBy = "dueDate", SortDirection = "asc",
            Page = 2, PageSize = 5
        };
        using var source = new CancellationTokenSource();
        c.Tasks.GetPagedAsync(c.WorkspaceId, query, source.Token).Returns([]);
        c.Tasks.CountAsync(c.WorkspaceId, query, source.Token).Returns(8);

        var response = await c.CreateTaskService().GetTasksAsync(c.WorkspaceId, query, source.Token);

        Assert.Equal(8, response.TotalCount);
        Assert.Equal(2, response.Page);
        Assert.Equal(5, response.PageSize);
        await c.Tasks.Received(1).GetPagedAsync(c.WorkspaceId, query, source.Token);
        await c.Tasks.Received(1).CountAsync(c.WorkspaceId, query, source.Token);
        await c.ShouldNotHaveCommittedAsync();
    }

    [Theory]
    [InlineData("sort")]
    [InlineData("enum")]
    [InlineData("dates")]
    [InlineData("page")]
    public async Task GetTasksAsync_WhenCalledDirectlyWithInvalidQuery_ShouldRejectBeforeRepositoryRead(string invalid)
    {
        var c = new ServiceTestContext().AsRole(WorkspaceRole.Member).MakeEveryReadThrow();
        var query = new GetTasksRequest();
        switch (invalid)
        {
            case "sort": query.SortBy = "unknown"; break;
            case "enum": query.Status = (TaskItemStatus)999; break;
            case "dates": query.DueDateFrom = DateTimeOffset.UtcNow; query.DueDateTo = query.DueDateFrom.Value.AddDays(-1); break;
            case "page": query.Page = 0; break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() => c.CreateTaskService().GetTasksAsync(c.WorkspaceId, query));

        await c.ShouldNotHaveCommittedAsync();
    }
}
