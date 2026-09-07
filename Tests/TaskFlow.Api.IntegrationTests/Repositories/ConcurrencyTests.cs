using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.IntegrationTests.Repositories;

public class ConcurrencyTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_WhenTaskWasChangedAfterLoad_ShouldRejectLoserAndRollBackActivity(bool winnerDeletes)
    {
        Tenant tenant = null!;
        Guid taskId = default;
        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "concurrency");
            taskId = AddTask(db, tenant, "Original").Id;
            return Task.CompletedTask;
        });
        await using var winner = CreateDbContext();
        await using var loser = CreateDbContext();
        var first = await winner.Tasks.SingleAsync(t => t.Id == taskId);
        var second = await loser.Tasks.SingleAsync(t => t.Id == taskId);
        if (winnerDeletes) first.Delete();
        else first.Start();
        second.Start();
        loser.TaskActivities.Add(new TaskActivity(tenant.WorkspaceId, taskId, tenant.OwnerId,
            TaskActivityAction.TaskStarted, "{}"));

        await new EfUnitOfWork(winner).SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            new EfUnitOfWork(loser).SaveChangesAsync());

        Assert.IsType<DbUpdateConcurrencyException>(error.InnerException);
        await using var verification = CreateDbContext();
        var saved = await verification.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(winnerDeletes, saved.IsDeleted);
        Assert.Equal(first.Status, saved.Status);
        Assert.Empty(await verification.TaskActivities.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_WhenProjectWasChangedAfterLoad_ShouldRejectStaleUpdateOrDelete(bool loserDeletes)
    {
        Guid projectId = default;
        await SeedAsync(db =>
        {
            projectId = AddTenant(db, "concurrency").ProjectId;
            return Task.CompletedTask;
        });
        await using var winner = CreateDbContext();
        await using var loser = CreateDbContext();
        var first = await winner.Projects.SingleAsync(p => p.Id == projectId);
        var second = await loser.Projects.SingleAsync(p => p.Id == projectId);
        first.UpdateDetails("Winner", "Saved");
        if (loserDeletes) second.Delete();
        else second.UpdateDetails("Loser", "Must not persist");

        await new EfUnitOfWork(winner).SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new EfUnitOfWork(loser).SaveChangesAsync());

        await using var verification = CreateDbContext();
        var saved = await verification.Projects.SingleAsync(p => p.Id == projectId);
        Assert.Equal("Winner", saved.Name);
        Assert.False(saved.IsDeleted);
        // A fresh read may deliberately apply a later edit; there is no hidden automatic retry.
        saved.UpdateDetails("Reviewed edit", "New request");
        await new EfUnitOfWork(verification).SaveChangesAsync();
    }
}
