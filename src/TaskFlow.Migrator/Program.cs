using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Persistence;

// A deployment command, not part of API startup. It needs database credentials,
// but neither a JWT signing key nor the SDK/EF CLI in its runtime image.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__DefaultConnection is required.");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

try
{
    var options = new DbContextOptionsBuilder<TaskFlowDbContext>()
        .UseNpgsql(connectionString).Options;
    await using var db = new TaskFlowDbContext(options);
    var pending = (await db.Database.GetPendingMigrationsAsync(cancellation.Token)).ToArray();
    Console.WriteLine($"Applying {pending.Length} pending migration(s).");
    await db.Database.MigrateAsync(cancellation.Token);
    Console.WriteLine("Database migrations completed successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Database migrations failed ({ex.GetType().Name}). Deployment must stop.");
    return 1;
}
