using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Workspaces;

public class AddWorkspaceMemberRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
