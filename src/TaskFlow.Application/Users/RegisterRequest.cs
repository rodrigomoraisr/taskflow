using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Users;

public class RegisterRequest : IValidatableObject
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(15)]
    public string Password { get; set; } = string.Empty;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!PasswordPolicy.IsValidNewPassword(Password))
            yield return new ValidationResult("Password must contain at least 15 characters, at most 72 UTF-8 bytes, and no control characters.", [nameof(Password)]);
    }
}
