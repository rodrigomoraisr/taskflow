using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);

        builder.Property(project => project.WorkspaceId)
            .IsRequired();

        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(project => project.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(project => project.IsDeleted)
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(project => project.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(project => new
        {
            project.WorkspaceId,
            project.IsDeleted
        });

        builder.HasAlternateKey(project => new
        {
            project.WorkspaceId,
            project.Id
        });
    }
}
