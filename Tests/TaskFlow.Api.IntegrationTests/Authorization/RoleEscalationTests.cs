using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.Authorization;

/// <summary>
/// The other half of the 403/404 decision: these callers ARE members, so 403
/// is correct and leaks nothing. Every test here asserts 403 — a 404 would be
/// wrong in the opposite direction, hiding a resource the caller can legitimately
/// see the existence of.
/// </summary>
public sealed class RoleEscalationTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    // ---------- Viewer ----------

    [Fact]
    public async Task UpdateTask_WhenCallerIsViewer_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var (projectId, taskId) = await SeedProjectAndTaskAsync(owner);
        var viewer = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Viewer);

        var response = await CreateClientFor(viewer).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}",
            new UpdateTaskRequest
            {
                Title = "Viewer edit",
                Description = "Should be refused",
                Priority = TaskPriority.Low
            });

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);
    }

    [Fact]
    public async Task StartTask_WhenCallerIsViewer_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var viewer = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Viewer);

        var response = await CreateClientFor(viewer).PostAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/start",
            content: null);

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);
    }

    [Fact]
    public async Task ListTasks_WhenCallerIsViewer_ShouldSucceed()
    {
        // The control case. Without this, the suite proves only that Viewer is
        // blocked, not that Viewer can still do the one thing it should.
        var owner = await RegisterUserAsync();
        await SeedProjectAndTaskAsync(owner);
        var viewer = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Viewer);

        var response = await CreateClientFor(viewer).GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks");

        response.EnsureSuccessStatusCode();
    }

    // ---------- Member ----------

    [Fact]
    public async Task CreateProject_WhenCallerIsMember_ShouldReturnForbidden()
    {
        // Project management is Owner/Admin only — Member editing tasks but not
        // creating projects is deliberate, and easy to break by accident.
        var owner = await RegisterUserAsync();
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(member).PostAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects",
            new CreateProjectRequest { Name = "Member project", Description = "x" });

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);
    }

    [Fact]
    public async Task AddMember_WhenCallerIsMember_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);
        var outsider = await RegisterUserAsync();

        var response = await CreateClientFor(member).PostAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members",
            new AddWorkspaceMemberRequest { Email = outsider.Email });

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);
    }

    [Fact]
    public async Task ChangeMemberRole_WhenCallerIsMemberPromotingThemselves_ShouldReturnForbidden()
    {
        // Self-promotion. If this passes, every role check in the system is
        // decorative.
        var owner = await RegisterUserAsync();
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(member).PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{member.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Owner });

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == member.Id);
            Assert.Equal(WorkspaceRole.Member, membership.Role);
        });
    }

    [Fact]
    public async Task UpdateTask_WhenCallerIsMember_ShouldSucceed()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(member).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}",
            new UpdateTaskRequest
            {
                Title = "Member edit",
                Description = "Permitted",
                Priority = TaskPriority.High
            });

        response.EnsureSuccessStatusCode();
    }

    // ---------- Assignment: the narrowest rule in the system ----------

    [Fact]
    public async Task AssignTask_WhenMemberClaimsUnassignedTaskForThemselves_ShouldSucceed()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(member).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/assignee",
            new AssignTaskRequest { UserId = member.Id });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AssignTask_WhenMemberAssignsSomeoneElse_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);
        var other = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(member).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/assignee",
            new AssignTaskRequest { UserId = other.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignTask_WhenMemberClaimsAlreadyAssignedTask_ShouldReturnForbidden()
    {
        // A Member may claim an unassigned task, not steal an assigned one.
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var firstClaim = await CreateClientFor(owner).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/assignee",
            new AssignTaskRequest { UserId = owner.Id });
        firstClaim.EnsureSuccessStatusCode();

        var steal = await CreateClientFor(member).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/assignee",
            new AssignTaskRequest { UserId = member.Id });

        Assert.Equal(HttpStatusCode.Forbidden, steal.StatusCode);
    }

    [Fact]
    public async Task UnassignTask_WhenCallerIsMember_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var claim = await CreateClientFor(member).PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/assignee",
            new AssignTaskRequest { UserId = member.Id });
        claim.EnsureSuccessStatusCode();

        var response = await CreateClientFor(member).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/assignee");

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);
    }

    // ---------- Admin vs Owner ----------

    [Fact]
    public async Task DeleteWorkspace_WhenCallerIsAdmin_ShouldReturnForbidden()
    {
        // Deleting a workspace is Owner-only. Admin is the interesting case:
        // it can manage everything else, which makes this boundary easy to lose.
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);

        var response = await CreateClientFor(admin).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}");

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);

        await WithDbAsync(async db =>
        {
            var workspace = await db.Workspaces.SingleAsync(w => w.Id == owner.WorkspaceId);
            Assert.False(workspace.IsDeleted);
        });
    }

    [Fact]
    public async Task CreateProject_WhenCallerIsAdmin_ShouldSucceed()
    {
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);

        var response = await CreateClientFor(admin).PostAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects",
            new CreateProjectRequest { Name = "Admin project", Description = "x" });

        response.EnsureSuccessStatusCode();
    }

    // ---------- What an Admin may do to other privileged members ----------
    //
    // The spec flagged these as untested because it had not read
    // EnsureCanManageTarget. It returns early for an Owner actor; for anyone
    // else it requires the target to be a Member or Viewer AND the requested
    // role not to be Owner. So an Admin is confined to managing regular
    // members and cannot mint another Owner. These tests pin that, since it is
    // the boundary that stops an Admin from quietly becoming an Owner.

    [Fact]
    public async Task RemoveMember_WhenAdminTargetsAnOwner_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);

        var response = await CreateClientFor(admin).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}");

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == owner.Id);
            Assert.False(membership.IsDeleted);
            Assert.Equal(WorkspaceRole.Owner, membership.Role);
        });
    }

    [Fact]
    public async Task ChangeMemberRole_WhenAdminDemotesAnOwner_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);

        var response = await CreateClientFor(admin).PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{owner.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Viewer });

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == owner.Id);
            Assert.Equal(WorkspaceRole.Owner, membership.Role);
        });
    }

    [Fact]
    public async Task ChangeMemberRole_WhenAdminPromotesSomeoneToOwner_ShouldReturnForbidden()
    {
        // The escalation-by-proxy route: an Admin who cannot promote itself
        // could otherwise promote an accomplice, or a second account.
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(admin).PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{member.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Owner });

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == member.Id);
            Assert.Equal(WorkspaceRole.Member, membership.Role);
        });
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTargetsAnotherAdmin_ShouldReturnForbidden()
    {
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);
        var otherAdmin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);

        var response = await CreateClientFor(admin).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{otherAdmin.Id}");

        await TenantResponses.ShouldBeForbiddenByRole(response, owner.WorkspaceId);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTargetsARegularMember_ShouldSucceed()
    {
        // The control. Without it, an Admin that could manage nobody at all
        // would pass every test above.
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(admin).DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{member.Id}");

        response.EnsureSuccessStatusCode();

        await WithDbAsync(async db =>
        {
            var membership = await db.WorkspaceUsers.SingleAsync(m =>
                m.WorkspaceId == owner.WorkspaceId && m.UserId == member.Id);
            Assert.True(membership.IsDeleted);
        });
    }

    [Fact]
    public async Task ChangeMemberRole_WhenAdminPromotesAMemberToAdmin_ShouldSucceed()
    {
        var owner = await RegisterUserAsync();
        var admin = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Admin);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);

        var response = await CreateClientFor(admin).PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{member.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Admin });

        response.EnsureSuccessStatusCode();
    }
}
