using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Opportunity : IValidatableObject
{
    public int OpportunityId { get; set; }

    [Required, MaxLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Required] public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }

    [Required, MaxLength(30)]
    public string Stage { get; set; } = "Qualification"; // Qualification,Proposal,Negotiation,Won,Lost

    public decimal Amount { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Open";

    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    [MaxLength(50)] public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public decimal WeightedPipeline => Amount * Probability / 100;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!OpportunityTransitions.IsValidStage(Stage))
            yield return new ValidationResult("Invalid stage.", new[] { nameof(Stage) });
        var isActive = Status != "Won" && Status != "Lost" && Stage != "Won" && Stage != "Lost";
        if (isActive && Amount <= 0)
            yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });
        if (Amount < 0)
            yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });
        if (isActive && ExpectedCloseDate.HasValue && ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}
