namespace TaskFlow.Application.Common.Interfaces;

public interface IAuthTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
