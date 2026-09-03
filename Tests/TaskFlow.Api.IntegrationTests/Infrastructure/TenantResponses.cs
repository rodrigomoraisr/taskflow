using System.Net;
using System.Net.Http.Json;

namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Assertions shared across the isolation suite.
///
/// The point of centralising these: the 403/404 rule is a security decision,
/// and if each test spells it out by hand then a future change has to be caught
/// in twenty places instead of one.
/// </summary>
public static class TenantResponses
{
    /// <summary>
    /// The exact body a caller gets for a workspace they cannot see, whether it
    /// does not exist or they are simply not a member. Must stay identical for
    /// both, or an outsider can probe for which workspaces are real.
    /// </summary>
    public static string WorkspaceNotFoundBody(Guid workspaceId)
        => $"The workspace {workspaceId} was not found.";

    /// <summary>
    /// Asserts the response is indistinguishable from "no such workspace":
    /// 404, and the canonical body. Used for every non-member access.
    /// </summary>
    public static async Task ShouldBeIndistinguishableFromMissingWorkspace(
        HttpResponseMessage response,
        Guid workspaceId)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<ErrorBody>();

        Assert.Equal(WorkspaceNotFoundBody(workspaceId), body!.Error);
    }

    /// <summary>
    /// Asserts the caller is a member whose role is too low: 403, and the body
    /// must not name the workspace or hint at membership state.
    /// </summary>
    public static async Task ShouldBeForbiddenByRole(
        HttpResponseMessage response,
        Guid workspaceId)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();

        Assert.DoesNotContain(
            workspaceId.ToString(),
            body!.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    public sealed record ErrorBody(string Error);
}
