using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tasks;

public class GetTaskResponse
{
    public Guid Id{get; set;}
    public string Title {get; set;} = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    public Guid ProjectId { get; set; }
    public TaskItemStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
}