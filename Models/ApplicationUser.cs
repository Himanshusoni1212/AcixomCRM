using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

// Identity-managed user. Passwords hashed by Identity (PBKDF2). Never expose hash.
public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
