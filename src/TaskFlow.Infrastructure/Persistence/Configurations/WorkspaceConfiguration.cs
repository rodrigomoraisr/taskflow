using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

public class WorkspaceConfiguration :
    IEntityTypeConfiguration<Workspace>
{
    public void Configure(
        EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.IsDeleted)
            .IsRequired();

        builder.Property(w => w.DeletedAt);

        builder.HasIndex(w => new
        {
            w.Name,
            w.IsDeleted
        });
    }
}