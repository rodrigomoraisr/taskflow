using System.ComponentModel.DataAnnotations;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Comments;

public sealed class WriteCommentRequest
{
    [Required, StringLength(Comment.MaxBodyLength, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
}
