using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.Authorization;

/// <summary>
/// A workspace with no owner is unrecoverable: nobody can delete it, nobody can
/// promote anyone, and support has to reach into the database. The invariant is
/// enforced by LastWorkspaceOwnerException, mapped to 409 Conflict — the state
/// of the resource forbids the change, rather than the caller lacking rights.
/// </summary>
public sealed class LastOwnerProtectionTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task RemoveMember_WhenTargetIsTheOnlyOwner_ShouldReturnConflict()
    {
        var owner = await RegisterUserAsync();

        var response = await CreateClientFor(owner).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == owner.Id);
            Assert.False(membership.IsDeleted);
            Assert.Equal(WorkspaceRole.Owner, membership.Role);
        });
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task ChangeMemberRole_WhenDemotingTheOnlyOwner_ShouldReturnConflict(
        WorkspaceRole target)
    {
        // Demotion is the sneakier route to an ownerless workspace: it looks
        // like an edit rather than a removal.
        var owner = await RegisterUserAsync();

        var response = await CreateClientFor(owner).PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = target });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == owner.Id);
            Assert.Equal(WorkspaceRole.Owner, membership.Role);
        });
    }

    [Fact]
    public async Task RemoveMember_WhenAnotherOwnerExists_ShouldSucceed()
    {
        // The control case: the rule protects the LAST owner, not owners in
        // general. Without this test, "always refuse" would pass the two above.
        var owner = await RegisterUserAsync();
        var secondOwner = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Owner);

        var response = await CreateClientFor(owner).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}");

        response.EnsureSuccessStatusCode();

        await WithDbAsync(async db =>
        {
            var remaining = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == secondOwner.Id);
            Assert.Equal(WorkspaceRole.Owner, remaining.Role);
            Assert.False(remaining.IsDeleted);
        });
    }

    [Fact]
    public async Task ChangeMemberRole_WhenDemotingOneOfTwoOwners_ShouldSucceed()
    {
        var owner = await RegisterUserAsync();
        await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Owner);

        var response = await CreateClientFor(owner).PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Admin });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RemoveMember_WhenRemovedOwnerWasTheSecondOfTwo_ShouldLeaveExactlyOneOwner()
    {
        // Guards against an off-by-one in the "another owner exists" count:
        // removing one of two owners must leave the workspace with one, and the
        // survivor must then be protected.
        var owner = await RegisterUserAsync();
        var secondOwner = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Owner);
        var ownerClient = CreateClientFor(owner);

        var firstRemoval = await ownerClient.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{secondOwner.Id}");
        firstRemoval.EnsureSuccessStatusCode();

        var secondRemoval = await ownerClient.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}");

        Assert.Equal(HttpStatusCode.Conflict, secondRemoval.StatusCode);
    }
}
