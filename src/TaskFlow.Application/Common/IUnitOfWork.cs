namespace TaskFlow.Application.Common;

public interface IUnitOfWork
{
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default
    );
}