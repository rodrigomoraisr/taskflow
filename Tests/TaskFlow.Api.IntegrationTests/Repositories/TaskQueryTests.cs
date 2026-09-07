using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.IntegrationTests.Repositories;

public sealed class TaskQueryTests(PostgreSqlFixture postgres) : RepositoryTestBase(postgres)
{
    [Theory]
    [InlineData("status", "Alpha,Delta")]
    [InlineData("priority", "Bravo,Charlie")]
    [InlineData("assignee", "Alpha,Charlie")]
    [InlineData("project", "Charlie")]
    [InlineData("from", "Bravo,Charlie")]
    [InlineData("to", "Alpha,Bravo")]
    [InlineData("unassigned", "Bravo,Delta")]
    [InlineData("combined", "Bravo")]
    public async Task Query_WhenFiltered_ShouldApplySamePredicatesToEveryPageAndCount(string filter, string expected)
    {
        Tenant tenant = default!;
        Guid secondProjectId = default;
        var boundary = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "A");
            var foreign = AddTenant(db, "B");
            var second = new Project(tenant.WorkspaceId, "Second", "");
            var deletedProject = new Project(tenant.WorkspaceId, "Deleted", "");
            deletedProject.Delete();
            db.Projects.AddRange(second, deletedProject);
            secondProjectId = second.Id;
            TaskItem Make(string title, Guid workspace, Guid project, TaskPriority priority, DateTime? due)
            {
                var task = new TaskItem(title, "", workspace, project, priority, due);
                db.Tasks.Add(task);
                return task;
            }
            var alpha = Make("Alpha", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.Low, boundary.AddDays(-10));
            alpha.AssignTo(tenant.OwnerId);
            Make("Bravo", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.High, boundary).Start();
            var charlie = Make("Charlie", tenant.WorkspaceId, second.Id, TaskPriority.High, boundary.AddDays(10));
            charlie.AssignTo(tenant.OwnerId);
            charlie.Complete();
            Make("Delta", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.Critical, null);
            Make("Deleted task", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.High, boundary).Delete();
            Make("Hidden parent", tenant.WorkspaceId, deletedProject.Id, TaskPriority.High, boundary);
            Make("Foreign", foreign.WorkspaceId, foreign.ProjectId, TaskPriority.High, boundary);
            return Task.CompletedTask;
        });
        var q = new GetTasksRequest { PageSize = 1, SortBy = "title", SortDirection = "asc" };
        switch (filter)
        {
            case "status": q.Status = TaskItemStatus.Todo; break;
            case "priority": q.Priority = TaskPriority.High; break;
            case "assignee": q.AssigneeUserId = tenant.OwnerId; break;
            case "project": q.ProjectId = secondProjectId; break;
            case "from": q.DueDateFrom = new DateTimeOffset(boundary); break;
            case "to": q.DueDateTo = new DateTimeOffset(boundary); break;
            case "unassigned": q.Unassigned = true; break;
            case "combined":
                q.Status = TaskItemStatus.InProgress;
                q.Priority = TaskPriority.High;
                q.ProjectId = tenant.ProjectId;
                q.Unassigned = true;
                // Equivalent offset instants must include the exact boundary.
                q.DueDateFrom = new DateTimeOffset(2026, 1, 20, 3, 0, 0, TimeSpan.FromHours(3));
                q.DueDateTo = new DateTimeOffset(boundary);
                break;
        }
        await using var context = CreateDbContext();
        var repository = new TaskRepository(context);

        var total = await repository.CountAsync(tenant.WorkspaceId, q);
        var found = new List<string>();
        for (q.Page = 1; q.Page <= total + 1; q.Page++)
        {
            var page = await repository.GetPagedAsync(tenant.WorkspaceId, q);
            Assert.All(page, t => Assert.Equal(tenant.WorkspaceId, t.WorkspaceId));
            found.AddRange(page.Select(t => t.Title));
        }

        Assert.Equal(expected.Split(','), found);
        Assert.Equal(found.Count, total);
    }

    [Theory]
    [InlineData("title", "asc", "Alpha,Middle,Zeta")]
    [InlineData("title", "desc", "Zeta,Middle,Alpha")]
    [InlineData("priority", "asc", "Zeta,Middle,Alpha")]
    [InlineData("priority", "desc", "Alpha,Middle,Zeta")]
    [InlineData("status", "asc", "Zeta,Middle,Alpha")]
    [InlineData("status", "desc", "Alpha,Middle,Zeta")]
    [InlineData("dueDate", "asc", "Alpha,Middle,Zeta")]
    [InlineData("dueDate", "desc", "Middle,Alpha,Zeta")]
    [InlineData("createdAt", "asc", "Alpha,Middle,Zeta")]
    [InlineData("createdAt", "desc", "Zeta,Middle,Alpha")]
    [InlineData("updatedAt", "asc", "Middle,Alpha,Zeta")]
    [InlineData("updatedAt", "desc", "Alpha,Middle,Zeta")]
    public async Task Query_WhenSorted_ShouldUseDocumentedOrderAndKeepNullDatesLast(string sort, string direction, string expected)
    {
        Tenant tenant = default!;
        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "sort");
            var day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var z = new TaskItem("Zeta", "", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.Low);
            var a = new TaskItem("Alpha", "", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.Critical, day);
            var m = new TaskItem("Middle", "", tenant.WorkspaceId, tenant.ProjectId, TaskPriority.Medium, day.AddDays(1));
            a.Complete();
            m.Start();
            db.Tasks.AddRange(z, a, m);
            db.Entry(z).Property(t => t.CreatedAt).CurrentValue = day.AddDays(2);
            db.Entry(a).Property(t => t.CreatedAt).CurrentValue = day;
            db.Entry(m).Property(t => t.CreatedAt).CurrentValue = day.AddDays(1);
            db.Entry(a).Property(t => t.UpdatedAt).CurrentValue = day.AddDays(1);
            db.Entry(m).Property(t => t.UpdatedAt).CurrentValue = day;
            return Task.CompletedTask;
        });
        await using var context = CreateDbContext();

        var tasks = await new TaskRepository(context).GetPagedAsync(tenant.WorkspaceId,
            new GetTasksRequest { SortBy = sort, SortDirection = direction });

        Assert.Equal(expected.Split(','), tasks.Select(t => t.Title));
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("title")]
    [InlineData("dueDate")]
    public async Task Query_WhenSortValuesTie_ShouldUseIdToPreventDuplicateOrMissingPages(string sort)
    {
        Tenant tenant = default!;
        var ids = new List<Guid>();
        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "ties");
            for (var i = 0; i < 5; i++)
            {
                var task = AddTask(db, tenant, "same");
                db.Entry(task).Property(t => t.CreatedAt).CurrentValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                ids.Add(task.Id);
            }
            return Task.CompletedTask;
        });
        await using var context = CreateDbContext();
        var repository = new TaskRepository(context);
        var found = new List<Guid>();

        for (var page = 1; page <= 3; page++)
            found.AddRange((await repository.GetPagedAsync(tenant.WorkspaceId,
                new GetTasksRequest { SortBy = sort, Page = page, PageSize = 2 })).Select(t => t.Id));

        Assert.Equal(ids.Order(), found);
    }

    [Fact]
    public async Task Query_WhenOffsetExceedsIntegerRange_ShouldReturnEmptyWithoutOverflow()
    {
        Tenant tenant = default!;
        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "large page");
            AddTask(db, tenant, "one");
            return Task.CompletedTask;
        });
        await using var context = CreateDbContext();
        var repository = new TaskRepository(context);
        var query = new GetTasksRequest { Page = int.MaxValue, PageSize = 100 };

        Assert.Empty(await repository.GetPagedAsync(tenant.WorkspaceId, query));
        Assert.Equal(1, await repository.CountAsync(tenant.WorkspaceId, query));
    }
}
