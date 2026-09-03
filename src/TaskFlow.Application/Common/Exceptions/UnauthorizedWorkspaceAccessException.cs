namespace TaskFlow.Application.Common.Exceptions;

/// <summary>
/// The current user has no active membership in the workspace they addressed.
///
/// Deliberately a different type from <see cref="WorkspaceNotFoundException"/>
/// so the application layer can still tell "you are not a member" apart from
/// "there is no such workspace". The API surface erases that difference: both
/// leave as the same 404, because confirming to an outsider that a workspace
/// exists is itself a tenant-boundary leak.
/// </summary>
public class UnauthorizedWorkspaceAccessException : Exception
{
    public UnauthorizedWorkspaceAccessException(Guid workspaceId)
        : base($"User does not belong to workspace {workspaceId}.")
    {
        WorkspaceId = workspaceId;
    }

    public Guid WorkspaceId { get; }
}
