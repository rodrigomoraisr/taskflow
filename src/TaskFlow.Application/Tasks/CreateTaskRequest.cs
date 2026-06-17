using System.ComponentModel.DataAnnotations;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tasks;

public class CreateTaskRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Description{get; set;} = string.Empty;
    public Guid WorkspaceId { get; set; }
    public Guid ProjectId { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
}