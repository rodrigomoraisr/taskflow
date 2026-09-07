using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public class TaskFlowDbContext : DbContext
{
    public TaskFlowDbContext(
        DbContextOptions<TaskFlowDbContext> options) : base(options)
    {
    }

    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<User> Users { get; set; }
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceUser> WorkspaceUsers => Set<WorkspaceUser>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureActivityIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureActivityIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureActivityIsAppendOnly()
    {
        if (ChangeTracker.Entries<TaskActivity>().Any(e =>
            e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Activity entries cannot be modified or deleted.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
