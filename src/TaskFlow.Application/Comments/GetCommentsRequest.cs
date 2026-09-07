using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Comments;

public sealed class GetCommentsRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
