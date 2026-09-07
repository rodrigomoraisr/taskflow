using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Activity;

public sealed record ActivityResponse(Guid Id, Guid TaskId, Guid ActorId,
    TaskActivityAction Action, Guid? CommentId, string Details, DateTime CreatedAt);
