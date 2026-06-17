using TaskFlow.Application.Common;

namespace TaskFlow.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly TaskFlowDbContext _dbContext;

    public EfUnitOfWork(TaskFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}