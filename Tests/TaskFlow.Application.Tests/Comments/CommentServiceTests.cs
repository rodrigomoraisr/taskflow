using NSubstitute;
using TaskFlow.Application.Comments;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Tests.TestSupport;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Comments;

public sealed class CommentServiceTests
{
    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Owner)]
    public async Task CreateAsync_WhenRoleCanComment_ShouldRecordActorAndCommitOnce(WorkspaceRole role)
    {
        var c = new ServiceTestContext().AsRole(role);
        var task = c.ArrangeTask();
        using var source = new CancellationTokenSource();

        var result = await c.CreateCommentService().CreateAsync(c.WorkspaceId, task.Id,
            new WriteCommentRequest { Body = "hello" }, source.Token);

        Assert.Equal(c.CallerId, result.AuthorId);
        await c.Comments.Received(1).AddAsync(Arg.Is<Comment>(x => x.Id == result.Id), source.Token);
        await c.Activities.Received(1).AddAsync(Arg.Is<TaskActivity>(a =>
            a.ActorId == c.CallerId && a.WorkspaceId == c.WorkspaceId
            && a.TaskId == task.Id && a.CommentId == result.Id
            && a.Action == TaskActivityAction.CommentCreated), source.Token);
        await c.UnitOfWork.Received(1).SaveChangesAsync(source.Token);
    }

    [Theory]
    [InlineData(WorkspaceRole.Member, true)]
    [InlineData(WorkspaceRole.Admin, true)]
    [InlineData(WorkspaceRole.Owner, true)]
    [InlineData(WorkspaceRole.Member, false)]
    [InlineData(WorkspaceRole.Admin, false)]
    [InlineData(WorkspaceRole.Owner, false)]
    public async Task MutateAsync_WhenNotAuthor_ShouldRefuseWithoutMutationOrActivity(WorkspaceRole role, bool edit)
    {
        var c = new ServiceTestContext().AsRole(role);
        var task = c.ArrangeTask();
        var comment = new Comment(c.WorkspaceId, task.Id, Guid.NewGuid(), "original");
        c.Comments.GetByIdAsync(comment.Id, task.Id, c.WorkspaceId, Arg.Any<CancellationToken>()).Returns(comment);
        var service = c.CreateCommentService();

        await Assert.ThrowsAsync<CommentOwnershipException>(() => edit
            ? service.EditAsync(c.WorkspaceId, task.Id, comment.Id, new WriteCommentRequest { Body = "changed" })
            : service.DeleteAsync(c.WorkspaceId, task.Id, comment.Id));

        Assert.Equal("original", comment.Body);
        Assert.False(comment.IsDeleted);
        Assert.Null(comment.UpdatedAt);
        await c.Activities.DidNotReceive().AddAsync(Arg.Any<TaskActivity>(), Arg.Any<CancellationToken>());
        await c.ShouldNotHaveCommittedAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MutateAsync_WhenAuthor_ShouldLoadBeforeOwnershipDecisionAndCommit(bool edit)
    {
        var c = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = c.ArrangeTask();
        var comment = new Comment(c.WorkspaceId, task.Id, c.CallerId, "original");
        c.Comments.GetByIdAsync(comment.Id, task.Id, c.WorkspaceId, Arg.Any<CancellationToken>()).Returns(comment);
        var service = c.CreateCommentService();

        if (edit) await service.EditAsync(c.WorkspaceId, task.Id, comment.Id, new WriteCommentRequest { Body = "changed" });
        else await service.DeleteAsync(c.WorkspaceId, task.Id, comment.Id);

        await c.Comments.Received(1).GetByIdAsync(comment.Id, task.Id, c.WorkspaceId, Arg.Any<CancellationToken>());
        await c.Activities.Received(1).AddAsync(Arg.Is<TaskActivity>(a => a.Action ==
            (edit ? TaskActivityAction.CommentEdited : TaskActivityAction.CommentDeleted)), Arg.Any<CancellationToken>());
        await c.ShouldHaveCommittedOnceAsync();
    }
}
