using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead
{
    public int LeadId { get; set; }

    [Required, MaxLength(20)]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(20)]
    [RegularExpression(@"^[0-9+\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    public string? Phone { get; set; }

    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(50)] public string? Source { get; set; }

    [Required(ErrorMessage = "Lead status is required.")]
    [MaxLength(30)]
    public string Status { get; set; } = "New"; // New,Contacted,Qualified,Unqualified,Converted,Lost

    [MaxLength(20)] public string? Priority { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Expected value is out of range.")]
    public decimal? ExpectedValue { get; set; }

    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? AssignedTo => OwnerId;
}
