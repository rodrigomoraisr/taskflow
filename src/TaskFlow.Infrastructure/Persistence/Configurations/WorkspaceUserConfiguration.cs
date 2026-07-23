using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

public class WorkspaceUserConfiguration :
    IEntityTypeConfiguration<WorkspaceUser>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceUser> builder)
    {
        builder.HasKey(wu => wu.Id);

        builder.Property(wu => wu.Role)
            .IsRequired();
        
        builder.Property(wu => wu.IsDeleted)
            .IsRequired();

        builder.HasIndex(
                wu => new
                {
                    wu.UserId,
                    wu.WorkspaceId
                })
            .IsUnique();

        builder.HasIndex(wu => wu.WorkspaceId);

        builder.HasIndex(wu => wu.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(wu => wu.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(wu => wu.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}