using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Projects;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.TenantIsolation;

public sealed class ProjectTenantIsolationTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetProject_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.GetAsync(
            $"/api/workspaces/{t.WorkspaceA}/projects/{t.ProjectA}");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task ListProjects_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.GetAsync(
            $"/api/workspaces/{t.WorkspaceA}/projects");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task CreateProject_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PostAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/projects",
            new CreateProjectRequest
            {
                Name = "Planted by another tenant",
                Description = "Should never exist"
            });

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);

        await WithDbAsync(async db =>
            Assert.False(await db.Projects
                .AnyAsync(p => p.Name == "Planted by another tenant")));
    }

    [Fact]
    public async Task UpdateProject_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PutAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/projects/{t.ProjectA}",
            new UpdateProjectRequest
            {
                Name = "Tampered",
                Description = "Should never persist",
                Status = ProjectStatus.Active
            });

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task DeleteProject_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.DeleteAsync(
            $"/api/workspaces/{t.WorkspaceA}/projects/{t.ProjectA}");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);

        await WithDbAsync(async db =>
        {
            var project = await db.Projects.SingleAsync(p => p.Id == t.ProjectA);
            Assert.False(project.IsDeleted);
        });
    }

    [Fact]
    public async Task GetProject_WhenProjectBelongsToAnotherWorkspace_ShouldNotBeReachableViaOwnWorkspace()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.GetAsync(
            $"/api/workspaces/{t.WorkspaceB}/projects/{t.ProjectA}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
