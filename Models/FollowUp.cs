using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class FollowUp : IValidatableObject
{
    public int FollowUpId { get; set; }

    [Required, MaxLength(20)]
    public string RelatedType { get; set; } = "Customer"; // Customer,Lead,Opportunity
    public int RelatedId { get; set; }

    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }

    [Required] public DateTime FollowUpDate { get; set; }

    [MaxLength(30)] public string? FollowUpType { get; set; }
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Status { get; set; } = "Planned"; // Planned,Completed,Missed,Cancelled

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public string? Remarks { get; set; }
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!FollowUpTransitions.IsValidRelatedType(RelatedType))
            yield return new ValidationResult("Related record type is required.", new[] { nameof(RelatedType) });
        if (!FollowUpTransitions.IsValidStatus(Status))
            yield return new ValidationResult("Follow-up status is required.", new[] { nameof(Status) });
        if ((Status == "Planned" || Status == "New") && FollowUpDate.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult("Follow-up date cannot be earlier than today.", new[] { nameof(FollowUpDate) });
    }
}
