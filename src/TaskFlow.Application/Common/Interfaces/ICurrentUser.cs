namespace TaskFlow.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid UserId { get; }
}