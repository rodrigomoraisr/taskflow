namespace TaskFlow.Api.OpenApi;

// HTTP documentation belongs in the API layer, not in application DTOs.
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequestExampleAttribute(string json) : Attribute
{
    public string Json { get; } = json;
}
