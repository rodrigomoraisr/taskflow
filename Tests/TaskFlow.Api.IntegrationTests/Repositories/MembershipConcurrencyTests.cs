using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.IntegrationTests.Repositories;

public sealed class MembershipConcurrencyTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    [Theory]
    [InlineData("remove")]
    [InlineData("demote")]
    [InlineData("revoke-caller")]
    public async Task MembershipChange_WhenOwnersChangeConcurrently_ShouldRetainOwnerAndRecheckAuthority(string operation)
    {
        Tenant tenant = null!;
        Guid secondOwner = default;
        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "Owner concurrency");
            secondOwner = AddMember(db, tenant, WorkspaceRole.Owner).Id;
            return Task.CompletedTask;
        });
        await using var firstDb = CreateDbContext();
        await using var secondDb = CreateDbContext();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRepository = new GatedRepository(firstDb, release.Task);
        var secondRepository = new GatedRepository(secondDb, Task.CompletedTask);
        var first = Service(firstDb, firstRepository, tenant.OwnerId);
        var second = Service(secondDb, secondRepository, secondOwner);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;

        Task Change(WorkspaceService service, Guid target) => operation == "demote"
            ? service.ChangeMemberRoleAsync(tenant.WorkspaceId, target,
                new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Member }, ct)
            : service.RemoveMemberAsync(tenant.WorkspaceId, target, ct);

        var winner = Change(first, operation == "revoke-caller" ? secondOwner : tenant.OwnerId);
        await firstRepository.Locked.Task.WaitAsync(ct);
        // Both requests passed their initial authorization before the first commits.
        var loser = Change(second, operation == "revoke-caller" ? tenant.OwnerId : secondOwner);
        try
        {
            await secondRepository.Entered.Task.WaitAsync(ct);
            Assert.False(loser.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }
        await winner;
        if (operation == "revoke-caller")
            await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(() => loser);
        else
            await Assert.ThrowsAsync<LastWorkspaceOwnerException>(() => loser);

        await using var verification = CreateDbContext();
        Assert.Equal(1, await verification.WorkspaceUsers.CountAsync(m =>
            m.WorkspaceId == tenant.WorkspaceId && m.Role == WorkspaceRole.Owner && !m.IsDeleted, ct));
    }

    private static WorkspaceService Service(TaskFlowDbContext db, IWorkspaceRepository workspaces, Guid userId)
    {
        var members = new WorkspaceUserRepository(db);
        return new WorkspaceService(workspaces, members,
            new WorkspaceAuthorizationService(new Caller(userId), members),
            new UserRepository(db), new EfUnitOfWork(db));
    }

    private sealed record Caller(Guid UserId) : ICurrentUser;

    // Gate the real PostgreSQL lock, not the authorization decision or owner count.
    private sealed class GatedRepository(TaskFlowDbContext db, Task release) : IWorkspaceRepository
    {
        private readonly WorkspaceRepository inner = new(db);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Locked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IApplicationTransaction> BeginMembershipChangeAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            var transaction = await inner.BeginMembershipChangeAsync(workspaceId, cancellationToken);
            Locked.TrySetResult();
            try { await release.WaitAsync(cancellationToken); }
            catch { await transaction.DisposeAsync(); throw; }
            return transaction;
        }
        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default) => inner.AddAsync(workspace, cancellationToken);
        public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<List<Workspace>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => inner.GetByIdsAsync(ids, cancellationToken);
    }
}
