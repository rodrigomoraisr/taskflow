using TaskFlow.Application.Exceptions;

namespace TaskFlow.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (TaskNotFoundException ex)
        {
            context.Response.StatusCode = 404;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = ex.Message
                });
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = "An unexpected error occurred."
                });
        }
    }
}