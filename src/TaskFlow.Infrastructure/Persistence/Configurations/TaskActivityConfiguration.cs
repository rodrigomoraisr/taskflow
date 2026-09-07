using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence.Configurations;

public sealed class TaskActivityConfiguration : IEntityTypeConfiguration<TaskActivity>
{
    public void Configure(EntityTypeBuilder<TaskActivity> builder)
    {
        builder.ToTable("task_activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Details).HasColumnType("jsonb").IsRequired();
        builder.HasOne<TaskItem>().WithMany()
            .HasForeignKey(a => new { a.WorkspaceId, a.TaskId })
            .HasPrincipalKey(t => new { t.WorkspaceId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Comment>().WithMany()
            .HasForeignKey(a => new { a.WorkspaceId, a.TaskId, a.CommentId })
            .HasPrincipalKey(c => new { c.WorkspaceId, c.TaskId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.WorkspaceId, a.CreatedAt, a.Id });
        builder.HasIndex(a => new { a.WorkspaceId, a.TaskId, a.CreatedAt, a.Id });
    }
}
