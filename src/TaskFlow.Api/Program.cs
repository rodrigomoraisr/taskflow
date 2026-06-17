using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Tasks;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddScoped<ITaskRepository,TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddDbContext<TaskFlowDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();