using TaskFlow.Application.Common;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Exceptions;

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
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Do not retry: that could overwrite the winner or duplicate activity.
            throw new ConcurrencyConflictException(ex);
        }
    }
}
