using NSubstitute;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tests.TestSupport;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Application.Tests.Projects;

/// <summary>
/// <c>ProjectService</c> orchestration, with the real
/// <c>WorkspaceAuthorizationService</c> underneath.
///
/// Projects have no separate authorization service of their own — the role
/// check is folded into <c>EnsureCanManageProjectsAsync</c> and
/// <c>EnsureCanViewProjectsAsync</c> — so these tests are also the direct
/// coverage of those two methods.
/// </summary>
public sealed class ProjectServiceTests
{
    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    public async Task CreateAsync_WhenTheRoleCanManageProjects_ShouldAddItInThatWorkspaceAndCommitOnce(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var service = context.CreateProjectService();

        // Act
        var response = await service.CreateAsync(
            context.WorkspaceId,
            new CreateProjectRequest
            {
                Name = "New project",
                Description = "Created by a service test"
            });

        // Assert
        await context.Projects
            .Received(1)
            .AddAsync(
                Arg.Is<Project>(project =>
                    project.WorkspaceId == context.WorkspaceId &&
                    project.Id == response.Id),
                Arg.Any<CancellationToken>());

        await context.ShouldHaveCommittedOnceAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task CreateAsync_WhenTheRoleCannotManageProjects_ShouldThrowAndNotCommit(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var service = context.CreateProjectService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.CreateAsync(
                context.WorkspaceId,
                new CreateProjectRequest
                {
                    Name = "Rejected project",
                    Description = "This role cannot create it"
                }));

        await context.Projects
            .DidNotReceive()
            .AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task CreateAsync_WhenTheCallerIsNotAMember_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsNonMember();
        var service = context.CreateProjectService();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => service.CreateAsync(
                context.WorkspaceId,
                new CreateProjectRequest
                {
                    Name = "Rejected project",
                    Description = "An outsider cannot create it"
                }));

        await context.ShouldNotHaveCommittedAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task GetByIdAsync_WhenTheCallerIsAnyMember_ShouldReadWithTheRequestWorkspaceId(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var project = context.ArrangeProject();
        var service = context.CreateProjectService();

        // Act
        var response = await service.GetByIdAsync(
            context.WorkspaceId,
            project.Id);

        // Assert
        Assert.Equal(project.Id, response.Id);

        await context.Projects
            .Received(1)
            .GetByIdAsync(
                project.Id,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheProjectIsInAnotherWorkspace_ShouldThrowProjectNotFound()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var service = context.CreateProjectService();

        // Act + Assert
        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => service.GetByIdAsync(context.WorkspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProjectsAsync_WhenTheCallerIsAMember_ShouldListWithTheRequestWorkspaceId()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);

        context.Projects
            .GetByWorkspaceAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns([
                new ProjectBuilder().InWorkspace(context.WorkspaceId).Build()
            ]);

        var service = context.CreateProjectService();

        // Act
        var response = await service.GetProjectsAsync(context.WorkspaceId);

        // Assert
        Assert.Single(response.Items);
        Assert.Equal(context.WorkspaceId, response.Items[0].WorkspaceId);

        await context.Projects
            .Received(1)
            .GetByWorkspaceAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenTheRoleCanManageProjects_ShouldUpdateAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Admin);
        var project = context.ArrangeProject();
        var service = context.CreateProjectService();

        // Act
        await service.UpdateAsync(
            context.WorkspaceId,
            project.Id,
            new UpdateProjectRequest
            {
                Name = "Renamed",
                Description = "Updated by a service test",
                Status = ProjectStatus.OnHold
            });

        // Assert
        Assert.Equal("Renamed", project.Name);
        Assert.Equal(ProjectStatus.OnHold, project.Status);

        await context.ShouldHaveCommittedOnceAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task UpdateAsync_WhenTheRoleCannotManageProjects_ShouldThrowAndLeaveItUntouched(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var project = context.ArrangeProject();
        var originalName = project.Name;
        var service = context.CreateProjectService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.UpdateAsync(
                context.WorkspaceId,
                project.Id,
                new UpdateProjectRequest
                {
                    Name = "Should not stick",
                    Description = "This role cannot write it",
                    Status = ProjectStatus.Completed
                }));

        Assert.Equal(originalName, project.Name);
        Assert.Equal(ProjectStatus.Active, project.Status);

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenTheProjectIsInAnotherWorkspace_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var foreignProjectId = Guid.NewGuid();
        var service = context.CreateProjectService();

        // Act + Assert
        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => service.UpdateAsync(
                context.WorkspaceId,
                foreignProjectId,
                new UpdateProjectRequest
                {
                    Name = "Should not stick",
                    Description = "Belongs to another tenant",
                    Status = ProjectStatus.Completed
                }));

        await context.Projects
            .Received(1)
            .GetByIdAsync(
                foreignProjectId,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheRoleCanManageProjects_ShouldSoftDeleteAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var project = context.ArrangeProject();
        var service = context.CreateProjectService();

        // Act
        await service.DeleteAsync(context.WorkspaceId, project.Id);

        // Assert
        Assert.True(project.IsDeleted);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task DeleteAsync_WhenTheRoleCannotManageProjects_ShouldThrowAndLeaveItActive(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var project = context.ArrangeProject();
        var service = context.CreateProjectService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.DeleteAsync(context.WorkspaceId, project.Id));

        Assert.False(project.IsDeleted);
        await context.ShouldNotHaveCommittedAsync();
    }

    /// <summary>
    /// A project the repository hands back already soft-deleted — which cannot
    /// happen today, because every repository filters those out first. Kept
    /// because the entity guard is the second half of that defence, and this is
    /// the only place the service's reaction to it can be observed.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenTheProjectIsAlreadyDeleted_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);

        var project = new ProjectBuilder()
            .InWorkspace(context.WorkspaceId)
            .Deleted()
            .Build();

        context.Projects
            .GetByIdAsync(
                project.Id,
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(project);

        var service = context.CreateProjectService();

        // Act + Assert
        await Assert
            .ThrowsAsync<TaskFlow.Domain.Exceptions.ProjectAlreadyDeletedException>(
                () => service.DeleteAsync(context.WorkspaceId, project.Id));

        await context.ShouldNotHaveCommittedAsync();
    }

}
