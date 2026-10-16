using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public class LoginDto
{
    [Required, EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterDto : IValidatableObject
{
    [Required, EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FullName { get; set; }

    // Defaults to SalesExecutive. Must be a known role; exactly one is assigned.
    public string Role { get; set; } = "SalesExecutive";

    public IEnumerable<ValidationResult> Validate(ValidationContext c)
    {
        if (!Models.Roles.All.Contains(Role))
            yield return new ValidationResult("Invalid role.", new[] { nameof(Role) });
    }
}
