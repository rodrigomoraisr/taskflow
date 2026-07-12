using TaskFlow.Application.Common.Exceptions;
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
        catch(UserAlreadyExistsException ex)
        {
            context.Response.StatusCode = 409;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = ex.Message
                });
        }
         catch(InvalidCredentialsException ex)
        {
            context.Response.StatusCode = 401;

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


// catch (Exception ex)
// {
//     var (statusCode, message) = ex switch
//     {
//         TaskNotFoundException => (404, ex.Message),
//         UserAlreadyExistsException => (409, ex.Message),
//         InvalidCredentialsException => (401, ex.Message),
//         _ => (500, "An unexpected error occurred.")
//     };

//     context.Response.StatusCode = statusCode;

//     await context.Response.WriteAsJsonAsync(new
//     {
//         error = message
//     });
// }