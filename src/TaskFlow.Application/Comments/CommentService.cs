using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Comments;

public sealed class CommentService(
    IWorkspaceAuthorizationService authorization,
    ITaskAuthorizationService taskAuthorization,
    ITaskRepository tasks,
    ICommentRepository comments,
    ITaskActivityRepository activities,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommentService
{
    public async Task<CommentResponse> CreateAsync(Guid workspaceId, Guid taskId,
        WriteCommentRequest request, CancellationToken cancellationToken = default)
    {
        await AuthorizeWriteAsync(workspaceId, cancellationToken);
        await EnsureTaskAsync(workspaceId, taskId, cancellationToken);
        var comment = new Comment(workspaceId, taskId, currentUser.UserId, request.Body);
        await comments.AddAsync(comment, cancellationToken);
        await RecordAndSaveAsync(comment, TaskActivityAction.CommentCreated, cancellationToken);
        return Map(comment);
    }

    public async Task<CommentResponse> GetByIdAsync(Guid workspaceId, Guid taskId,
        Guid id, CancellationToken cancellationToken = default)
    {
        await authorization.EnsureCanViewWorkspaceAsync(workspaceId, cancellationToken);
        await EnsureTaskAsync(workspaceId, taskId, cancellationToken);
        return Map(await LoadAsync(workspaceId, taskId, id, cancellationToken));
    }

    public async Task<List<CommentResponse>> GetPagedAsync(Guid workspaceId, Guid taskId,
        GetCommentsRequest request, CancellationToken cancellationToken = default)
    {
        await authorization.EnsureCanViewWorkspaceAsync(workspaceId, cancellationToken);
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            throw new ArgumentException("Invalid pagination.");
        await EnsureTaskAsync(workspaceId, taskId, cancellationToken);
        var rows = await comments.GetPagedAsync(taskId, workspaceId,
            request.Page, request.PageSize, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task EditAsync(Guid workspaceId, Guid taskId, Guid id,
        WriteCommentRequest request, CancellationToken cancellationToken = default)
    {
        var comment = await LoadOwnedAsync(workspaceId, taskId, id, cancellationToken);
        comment.Edit(request.Body);
        await RecordAndSaveAsync(comment, TaskActivityAction.CommentEdited, cancellationToken);
    }

    public async Task DeleteAsync(Guid workspaceId, Guid taskId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var comment = await LoadOwnedAsync(workspaceId, taskId, id, cancellationToken);
        comment.Delete();
        await RecordAndSaveAsync(comment, TaskActivityAction.CommentDeleted, cancellationToken);
    }

    private async Task AuthorizeWriteAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var role = await authorization.GetActiveRoleAsync(workspaceId, cancellationToken);
        taskAuthorization.EnsureCanEdit(role);
    }

    private async Task EnsureTaskAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken)
    {
        if (await tasks.GetByIdAsync(taskId, workspaceId, cancellationToken) is null)
            throw new TaskNotFoundException(taskId);
    }

    private async Task<Comment> LoadAsync(Guid workspaceId, Guid taskId, Guid id,
        CancellationToken cancellationToken)
    {
        return await comments.GetByIdAsync(id, taskId, workspaceId, cancellationToken)
            ?? throw new CommentNotFoundException(id);
    }

    private async Task<Comment> LoadOwnedAsync(Guid workspaceId, Guid taskId,
        Guid id, CancellationToken cancellationToken)
    {
        await AuthorizeWriteAsync(workspaceId, cancellationToken);
        await EnsureTaskAsync(workspaceId, taskId, cancellationToken);
        var comment = await LoadAsync(workspaceId, taskId, id, cancellationToken);
        if (comment.AuthorId != currentUser.UserId)
            throw new CommentOwnershipException();
        return comment;
    }

    private async Task RecordAndSaveAsync(Comment comment, TaskActivityAction action,
        CancellationToken cancellationToken)
    {
        await activities.AddAsync(new TaskActivity(comment.WorkspaceId, comment.TaskId,
            currentUser.UserId, action, "{}", comment.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static CommentResponse Map(Comment c) =>
        new(c.Id, c.TaskId, c.AuthorId, c.Body, c.CreatedAt, c.UpdatedAt);
}
