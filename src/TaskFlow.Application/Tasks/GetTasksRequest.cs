using System.ComponentModel.DataAnnotations;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tasks;

public class GetTasksRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus? Status { get; set; }
    [EnumDataType(typeof(TaskPriority))]
    public TaskPriority? Priority { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public bool Unassigned { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTimeOffset? DueDateFrom { get; set; }
    public DateTimeOffset? DueDateTo { get; set; }

    [Required, RegularExpression("(?i)^(createdAt|updatedAt|title|priority|status|dueDate)$")]
    public string SortBy { get; set; } = "createdAt";
    [Required, RegularExpression("(?i)^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AssigneeUserId == Guid.Empty)
            yield return new("Assignee ID must not be empty.", [nameof(AssigneeUserId)]);
        if (ProjectId == Guid.Empty)
            yield return new("Project ID must not be empty.", [nameof(ProjectId)]);
        if (Unassigned && AssigneeUserId.HasValue)
            yield return new("Choose an assignee or unassigned tasks, not both.", [nameof(Unassigned), nameof(AssigneeUserId)]);
        if (DueDateFrom > DueDateTo)
            yield return new("DueDateFrom must not exceed DueDateTo.", [nameof(DueDateFrom), nameof(DueDateTo)]);
    }

    // Services can be called outside HTTP; keep the same validation there.
    public void EnsureValid()
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(this, new ValidationContext(this), errors, true))
            throw new ArgumentException(string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
}
