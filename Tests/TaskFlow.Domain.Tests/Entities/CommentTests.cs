using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Tests.Entities;

public sealed class CommentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenBodyIsBlank_ShouldRejectIt(string? body)
    {
        Assert.Throws<ArgumentException>(() => Create(body!));
    }

    [Fact]
    public void Constructor_WhenBodyExceedsLimit_ShouldRejectIt()
    {
        Assert.Throws<ArgumentException>(() => Create(new string('x', 2001)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenAnIdentityIsEmpty_ShouldRejectIt(int index)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[index] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => new Comment(ids[0], ids[1], ids[2], "Hello"));
    }

    [Fact]
    public void Edit_WhenBodyIsInvalid_ShouldLeaveCommentUnchanged()
    {
        var comment = Create("original");

        Assert.Throws<ArgumentException>(() => comment.Edit(new string('x', 2001)));

        Assert.Equal("original", comment.Body);
        Assert.Null(comment.UpdatedAt);
    }

    [Fact]
    public void Edit_WhenActive_ShouldTrimBodyAndKeepAuthorship()
    {
        var comment = Create(new string('x', 2000));
        var author = comment.AuthorId;

        comment.Edit("  revised  ");

        Assert.Equal("revised", comment.Body);
        Assert.Equal(author, comment.AuthorId);
        Assert.NotNull(comment.UpdatedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mutate_WhenDeleted_ShouldThrowMappedException(bool edit)
    {
        var comment = Create("hello");
        comment.Delete();

        Assert.Throws<CommentAlreadyDeletedException>(() =>
        {
            if (edit) comment.Edit("new");
            else comment.Delete();
        });

        Assert.True(comment.IsDeleted);
        Assert.NotNull(comment.DeletedAt);
        Assert.Equal("hello", comment.Body);
    }

    private static Comment Create(string body) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), body);
}
