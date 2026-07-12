using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskFlow.Infrastructure.Persistence;

public class TaskFlowDbContextFactory
    : IDesignTimeDbContextFactory<TaskFlowDbContext>
{
    public TaskFlowDbContext CreateDbContext(
        string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<TaskFlowDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=taskflow;Username=postgres;Password=postgres");

        return new TaskFlowDbContext(
            optionsBuilder.Options);
    }
}