using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace TaskFlow.Api.Middleware;

public static class ApiProblems
{
    public static void Customize(HttpContext context, ProblemDetails problem)
    {
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        context.Response.Headers.CacheControl = "no-store";
    }

    public static Task WriteAsync(HttpContext context, int status, string? detail = null,
        CancellationToken cancellationToken = default)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = ReasonPhrases.GetReasonPhrase(status),
            Status = status,
            Detail = detail
        };
        Customize(context, problem);
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
    }
}
