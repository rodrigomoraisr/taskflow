namespace TaskFlow.Application.Common.Exceptions;

public sealed class CommentNotFoundException(Guid id)
    : Exception($"Comment {id} was not found.");
