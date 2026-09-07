namespace TaskFlow.Application.Common.Exceptions;

public sealed class CommentOwnershipException()
    : Exception("Only the comment author can modify or delete it.");
