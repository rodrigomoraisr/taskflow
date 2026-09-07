using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Tests.Entities;

public sealed class TaskActivityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenIdentityIsEmpty_ShouldRejectIt(int index)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[index] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => new TaskActivity(ids[0], ids[1], ids[2],
            TaskActivityAction.TaskCreated, "{}"));
    }

    [Fact]
    public void Constructor_WhenActionIsUndefined_ShouldRejectIt()
    {
        Assert.Throws<ArgumentException>(() => Create((TaskActivityAction)999));
    }

    [Theory]
    [InlineData(TaskActivityAction.CommentCreated)]
    [InlineData(TaskActivityAction.CommentEdited)]
    [InlineData(TaskActivityAction.CommentDeleted)]
    public void Constructor_WhenCommentActionLacksCommentId_ShouldRejectIt(TaskActivityAction action)
    {
        Assert.Throws<ArgumentException>(() => Create(action));
        var id = Guid.NewGuid();
        Assert.Equal(id, Create(action, id).CommentId);
    }

    [Fact]
    public void Constructor_WhenTaskActionHasCommentId_ShouldRejectIt()
    {
        Assert.Throws<ArgumentException>(() => Create(TaskActivityAction.TaskCreated, Guid.NewGuid()));
    }

    private static TaskActivity Create(TaskActivityAction action, Guid? commentId = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), action, "{}", commentId);
}
