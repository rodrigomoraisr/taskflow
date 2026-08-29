using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Projects;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectService(
        IProjectRepository projectRepository,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateProjectResponse> CreateAsync(
        Guid workspaceId,
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanManageProjectsAsync(
            workspaceId,
            cancellationToken);

        var project = new Project(
            workspaceId,
            request.Name,
            request.Description);

        await _projectRepository.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateProjectResponse
        {
            Id = project.Id
        };
    }

    public async Task<GetProjectResponse> GetByIdAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanViewProjectsAsync(
            workspaceId,
            cancellationToken);

        var project = await GetProjectAsync(
            projectId,
            workspaceId,
            cancellationToken);

        return Map(project);
    }

    public async Task<GetProjectsResponse> GetProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanViewProjectsAsync(
            workspaceId,
            cancellationToken);

        var projects = await _projectRepository.GetByWorkspaceAsync(
            workspaceId,
            cancellationToken);

        return new GetProjectsResponse
        {
            Items = projects.Select(Map).ToList()
        };
    }

    public async Task UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanManageProjectsAsync(
            workspaceId,
            cancellationToken);

        var project = await GetProjectAsync(
            projectId,
            workspaceId,
            cancellationToken);

        project.UpdateDetails(request.Name, request.Description);
        project.ChangeStatus(request.Status!.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanManageProjectsAsync(
            workspaceId,
            cancellationToken);

        var project = await GetProjectAsync(
            projectId,
            workspaceId,
            cancellationToken);

        project.Delete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Project> GetProjectAsync(
        Guid projectId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            projectId,
            workspaceId,
            cancellationToken);

        if (project is null)
            throw new ProjectNotFoundException(projectId);

        return project;
    }

    private static GetProjectResponse Map(Project project)
    {
        return new GetProjectResponse
        {
            Id = project.Id,
            WorkspaceId = project.WorkspaceId,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status.ToString(),
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}
