using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Api.OpenApi;

namespace TaskFlow.Api.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class OpenApiDocumentationTests
{
    [Fact]
    public async Task Document_ShouldCoverEveryControllerActionWithExamplesAndCorrectAuth()
    {
        await using var factory = new TaskFlowApiFactory("Host=localhost;Database=unused");
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync();
        var actions = scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();
        var operations = document.Paths.Values.SelectMany(path => path.Operations?.Values.AsEnumerable() ?? []).ToArray();

        Assert.Equal(actions.Length + 2, operations.Length);
        foreach (var action in actions)
        {
            var operation = Assert.Single(operations, op => op.OperationId == $"{action.ControllerName}_{action.ActionName}");
            Assert.False(string.IsNullOrWhiteSpace(operation.Summary));
            Assert.Contains("Example request", operation.Description);
            Assert.Equal(action.ControllerName != "Auth", operation.Security?.Count > 0);
            Assert.NotNull(operation.Responses);
            Assert.Contains("429", operation.Responses.Keys);
            Assert.Contains("500", operation.Responses.Keys);
            foreach (var response in operation.Responses.Where(r => int.Parse(r.Key) >= 400))
            {
                Assert.NotNull(response.Value.Content);
                var media = Assert.Single(response.Value.Content);
                Assert.Equal("application/problem+json", media.Key);
                Assert.NotNull(media.Value.Example);
            }
            if (operation.RequestBody is not null)
            {
                Assert.NotNull(operation.RequestBody.Content);
                Assert.All(operation.RequestBody.Content.Values, media => Assert.NotNull(media.Example));
                var example = action.MethodInfo.GetCustomAttribute<RequestExampleAttribute>();
                Assert.NotNull(example);
                var parameter = action.MethodInfo.GetParameters().Single(p => p.ParameterType.Name.EndsWith("Request") &&
                    !p.ParameterType.Name.StartsWith("Get"));
                var value = JsonSerializer.Deserialize(example.Json, parameter.ParameterType, JsonSerializerOptions.Web);
                Assert.NotNull(value);
                var errors = new List<ValidationResult>();
                Assert.True(Validator.TryValidateObject(value, new ValidationContext(value), errors, true),
                    string.Join("; ", errors.Select(e => e.ErrorMessage)));
            }
        }
    }

    [Fact]
    public async Task Document_ShouldDescribeCreationNoContentAndProbeResponses()
    {
        await using var factory = new TaskFlowApiFactory("Host=localhost;Database=unused");
        using var scope = factory.Services.CreateScope();
        var document = await scope.ServiceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1")
            .GetOpenApiDocumentAsync();
        var operations = document.Paths.Values.SelectMany(path => path.Operations?.Values.AsEnumerable() ?? []).ToArray();
        var register = Assert.Single(operations, op => op.OperationId == "Auth_Register");
        Assert.NotNull(register.Responses);
        Assert.Contains("201", register.Responses.Keys);
        Assert.DoesNotContain("200", register.Responses.Keys);
        var logout = Assert.Single(operations, op => op.OperationId == "Auth_Logout");
        Assert.NotNull(logout.Responses);
        Assert.Contains("204", logout.Responses.Keys);
        Assert.True(logout.Responses["204"].Content is null or { Count: 0 });
        var ready = Assert.Single(operations, op => op.OperationId == "Health_ready");
        Assert.NotNull(ready.Responses);
        Assert.Contains("503", ready.Responses.Keys);
        Assert.False(Assert.Single(operations, op => op.OperationId == "Health_live").Security?.Count > 0);
        Assert.NotNull(document.Components);
        Assert.NotNull(document.Components.SecuritySchemes);
        Assert.Equal("bearer", document.Components.SecuritySchemes["Bearer"].Scheme);
    }
}
