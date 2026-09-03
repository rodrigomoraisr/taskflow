using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.TenantIsolation;

/// <summary>
/// Membership is the most dangerous surface in the system: a caller who can add
/// themselves to a workspace has bypassed every other check in this suite.
/// </summary>
public sealed class MembershipTenantIsolationTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task ListMembers_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.GetAsync(
            $"/api/workspaces/{t.WorkspaceA}/members");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task AddMember_WhenCallerTriesToAddThemselvesToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        // The privilege-escalation attempt this whole suite exists to catch.
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PostAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/members",
            new AddWorkspaceMemberRequest { Email = t.OwnerB.Email });

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);

        await WithDbAsync(async db =>
            Assert.False(await db.WorkspaceUsers.AnyAsync(m =>
                m.WorkspaceId == t.WorkspaceA &&
                m.UserId == t.OwnerB.Id)));
    }

    [Fact]
    public async Task RemoveMember_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.DeleteAsync(
            $"/api/workspaces/{t.WorkspaceA}/members/{t.OwnerA.Id}");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == t.WorkspaceA && m.UserId == t.OwnerA.Id);
            Assert.False(membership.IsDeleted);
        });
    }

    [Fact]
    public async Task ChangeMemberRole_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PatchAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/members/{t.OwnerA.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Viewer });

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task DeleteWorkspace_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.DeleteAsync(
            $"/api/workspaces/{t.WorkspaceA}");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);

        await WithDbAsync(async db =>
        {
            var workspace = await db.Workspaces.SingleAsync(w => w.Id == t.WorkspaceA);
            Assert.False(workspace.IsDeleted);
        });
    }

    [Fact]
    public async Task ListWorkspaces_WhenCallerHasOneWorkspace_ShouldNotIncludeOtherTenants()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerA = CreateClientFor(t.OwnerA);

        var response = await callerA.GetAsync("/api/workspaces");
        response.EnsureSuccessStatusCode();

        var workspaces = await response.Content
            .ReadFromJsonAsync<List<ListWorkspaceResponse>>();

        Assert.Contains(workspaces!, w => w.Id == t.WorkspaceA);
        Assert.DoesNotContain(workspaces!, w => w.Id == t.WorkspaceB);
    }

    [Fact]
    public async Task RemovedMember_WhenAccessingWorkspaceAfterRemoval_ShouldReturnCanonicalNotFound()
    {
        // A revoked membership must take effect immediately. The token still
        // validates — it only carries identity — so the role lookup is the
        // only thing standing between an ex-member and the data.
        var owner = await RegisterUserAsync();
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var memberClient = CreateClientFor(member);
        var beforeRemoval = await memberClient.GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks");
        beforeRemoval.EnsureSuccessStatusCode();

        var ownerClient = CreateClientFor(owner);
        var removal = await ownerClient.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{member.Id}");
        removal.EnsureSuccessStatusCode();

        var afterRemoval = await memberClient.GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                afterRemoval, owner.WorkspaceId);
    }
}
