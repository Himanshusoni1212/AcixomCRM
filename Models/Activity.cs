using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Activity : IValidatableObject
{
    public int ActivityId { get; set; }

    [Required, MaxLength(30)]
    public string ActivityType { get; set; } = "Task"; // Call,Meeting,Email,Task

    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Required] public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    [Required, MaxLength(30)] public string Status { get; set; } = "Planned";

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!ActivityTypes.IsValidType(ActivityType))
            yield return new ValidationResult("Activity type is required.", new[] { nameof(ActivityType) });
        if (!ActivityTypes.IsValidStatus(Status))
            yield return new ValidationResult("Activity status is required.", new[] { nameof(Status) });
    }
}
