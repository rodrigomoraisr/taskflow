using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly TaskFlowDbContext _dbContext;

    public ProjectRepository(TaskFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Projects.AddAsync(project, cancellationToken);
    }

    public async Task<Project?> GetByIdAsync(
        Guid projectId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects.FirstOrDefaultAsync(
            project => project.Id == projectId &&
                       project.WorkspaceId == workspaceId &&
                       !project.IsDeleted,
            cancellationToken);
    }

    public async Task<List<Project>> GetByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId &&
                              !project.IsDeleted)
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);
    }
}
