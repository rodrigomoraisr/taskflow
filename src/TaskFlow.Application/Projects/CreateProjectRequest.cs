using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Projects;

public class CreateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;
}
