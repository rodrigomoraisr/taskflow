using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in-process, pointed at the Testcontainers PostgreSQL
/// instance. Requests go through the genuine pipeline — routing, model
/// validation, authentication, ExceptionMiddleware — so a test sees exactly
/// what a caller would.
/// </summary>
public sealed class TaskFlowApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Long enough to satisfy the 32-byte minimum the startup guard enforces.
    /// A test signing key, never a real one.
    /// </summary>
    public const string SigningKey =
        "taskflow-integration-tests-signing-key-not-a-secret";

    public TaskFlowApiFactory(string connectionString)
    {
        // Program.cs reads Jwt:Key and the connection string from
        // builder.Configuration *before* builder.Build(). ConfigureWebHost runs
        // later than that, so injecting config there would arrive after the
        // startup guard has already thrown. Environment variables are part of
        // the default configuration sources and are therefore the only hook
        // that is in place early enough.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "TaskFlow");
        Environment.SetEnvironmentVariable("Jwt__Audience", "TaskFlowUsers");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");

        // Not Development: that would load whichever developer's user secrets
        // happen to be on the machine and make the suite depend on them.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
