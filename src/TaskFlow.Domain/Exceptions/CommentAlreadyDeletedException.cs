namespace TaskFlow.Domain.Exceptions;

public sealed class CommentAlreadyDeletedException(Guid id)
    : Exception($"Comment {id} is already deleted.");
