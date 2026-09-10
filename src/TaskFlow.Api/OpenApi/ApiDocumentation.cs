using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TaskFlow.Api.OpenApi;

public static class ApiDocumentation
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (context.Description.ActionDescriptor is ControllerActionDescriptor action)
            {
                operation.OperationId = $"{action.ControllerName}_{action.ActionName}";
                var example = action.MethodInfo.GetCustomAttributes(typeof(RequestExampleAttribute), false)
                    .Cast<RequestExampleAttribute>().SingleOrDefault();
                if (example is not null && operation.RequestBody?.Content is { } content)
                    foreach (var media in content.Values)
                        media.Example = JsonNode.Parse(example.Json);
            }

            if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                }];

            if (operation.Responses is not null)
                foreach (var (status, response) in operation.Responses)
                {
                    response.Description = status switch
                    {
                        "201" => "Created. See the response body for the new identifier.",
                        "204" => "Completed successfully; no response body.",
                        "400" => "Invalid input. Validation failures include an errors map in ProblemDetails.",
                        "401" => "Authentication failed: missing/expired access JWT or invalid credentials/refresh token, as applicable.",
                        "403" => "Workspace membership exists, but role, ownership or assignment rules refuse this action.",
                        "404" => "Resource is absent, soft-deleted, or unavailable to this workspace member. Non-members cannot distinguish an unknown workspace from a forbidden one.",
                        "409" => "Conflict with current state: duplicate, invalid transition, last-owner rule or overlapping write, as applicable.",
                        "429" => "Request budget exhausted. Retry-After indicates the wait when available.",
                        "500" => "Unexpected server error; use correlationId to locate server logs.",
                        _ => response.Description
                    };
                    if (response.Content?.TryGetValue("application/json", out var json) == true)
                    {
                        if (int.TryParse(status, out var code) && code >= 400)
                        {
                            json.Example = new JsonObject
                            {
                                ["type"] = "about:blank",
                                ["title"] = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(code),
                                ["status"] = code,
                                ["correlationId"] = "example-request-001"
                            };
                            response.Content.Clear();
                            response.Content["application/problem+json"] = json;
                        }
                        else
                            response.Content.Remove("text/plain");
                    }
                }
            return Task.CompletedTask;
        });

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "TaskFlow API",
                Version = "v1",
                Description = "Multi-tenant task management. Register, log in, then send Authorization: Bearer <access-token>. " +
                    "Choose workspaceId in the route; membership is checked per request. IDs in examples are placeholders. " +
                    "Enum-typed fields use JSON numbers; project-status and membership-role response fields are strings. " +
                    "Errors use application/problem+json with correlationId. " +
                    "Production hosts the API but does not expose this development OpenAPI endpoint."
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token returned by /auth/login or /auth/refresh. Do not use the refresh token here."
            };
            // Health-check middleware has no ApiExplorer entry. Describe its existing contract here.
            foreach (var probe in new[] { "live", "ready" })
            {
                var responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse
                    {
                        Description = "Healthy",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["text/plain"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Example = JsonValue.Create("Healthy") }
                        }
                    }
                };
                if (probe == "ready")
                    responses["503"] = new OpenApiResponse
                    {
                        Description = "Database unavailable or health check timed out.",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["text/plain"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Example = JsonValue.Create("Unhealthy") }
                        }
                    };
                document.Paths[$"/health/{probe}"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new()
                        {
                            OperationId = $"Health_{probe}",
                            Summary = probe == "live" ? "Check process liveness" : "Check database readiness",
                            Description = $"Anonymous and exempt from rate limiting. Example: `GET /health/{probe}`. " +
                                (probe == "live" ? "Does not query the database; suitable for recurring probes." : "Checks database connectivity, not business permissions or schema correctness."),
                            Responses = responses
                        }
                    }
                };
            }
            return Task.CompletedTask;
        });
    }
}
