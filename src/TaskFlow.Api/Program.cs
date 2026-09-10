using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using System.Globalization;
using TaskFlow.Api.Middleware;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Security;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Infrastructure.Libraries;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaskFlow.Application.Workspaces;
using TaskFlow.Api.Security;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Projects;

using TaskFlow.Application.Comments;
using TaskFlow.Application.Activity;
using TaskFlow.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTrustedProxyHeaders(builder.Configuration);
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(TaskFlow.Api.OpenApi.ApiDocumentation.Configure);
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        ApiProblems.Customize(context.HttpContext, context.ProblemDetails));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddSingleton<IRefreshTokenCodec, RefreshTokenCodec>();
builder.Services.AddOptions<AuthRateLimitOptions>()
    .Bind(builder.Configuration.GetSection("AuthRateLimit"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<GlobalRateLimitOptions>()
    .Bind(builder.Configuration.GetSection("GlobalRateLimit"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<GlobalRateLimitOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.PermitLimit,
                Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string, AuthRateLimitPolicy>("auth");
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        await ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
            "Too many requests. Try again later.", cancellationToken);
    };
});

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IWorkspaceAuthorizationService, WorkspaceAuthorizationService>();
builder.Services.AddScoped<ITaskAuthorizationService, TaskAuthorizationService>();

builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ITaskActivityRepository, TaskActivityRepository>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IActivityService, ActivityService>();

builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectService, ProjectService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IWorkspaceUserRepository, WorkspaceUserRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddDbContext<TaskFlowDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// The signing key never has a default. A missing key must stop the application
// here rather than let it start and issue tokens anyone could forge. In
// development it comes from user secrets; in CI and production from Jwt__Key.
// See the Security section of README.md.
var jwtKey = JwtSigningKey.Validate(builder.Configuration["Jwt:Key"]);

if (!int.TryParse(builder.Configuration["Jwt:ExpirationMinutes"], out var accessMinutes)
    || accessMinutes is < 1 or > 15)
    throw new InvalidOperationException("Jwt:ExpirationMinutes must be between 1 and 15.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey))
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseMiddleware<ProxyDiagnosticsMiddleware>();
// Establish scheme/client address before redirects, logging and IP rate limits.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RequestObservabilityMiddleware>();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/auth"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
    await next(context);
});
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages(context => ApiProblems.WriteAsync(context.HttpContext,
    context.HttpContext.Response.StatusCode, cancellationToken: context.HttpContext.RequestAborted));

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Probes must stay usable when application clients exhaust their request budget.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous().DisableRateLimiting();

app.Run();

/// <summary>
/// Program is implicit with top-level statements and therefore internal, which
/// WebApplicationFactory&lt;Program&gt; in the test assembly cannot see. Declaring
/// the partial makes it public without adding a line of behaviour.
/// </summary>
public partial class Program
{
}
