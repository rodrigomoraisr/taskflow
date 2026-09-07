using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Body).HasMaxLength(Comment.MaxBodyLength).IsRequired();
        builder.HasOne<TaskItem>().WithMany()
            .HasForeignKey(c => new { c.WorkspaceId, c.TaskId })
            .HasPrincipalKey(t => new { t.WorkspaceId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.WorkspaceId, c.TaskId, c.IsDeleted, c.CreatedAt, c.Id });
    }
}
