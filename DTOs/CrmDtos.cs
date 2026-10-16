using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public class CustomerDto : IValidatableObject
{
    [Required]
    [MaxLength(20)] public string CustomerCode { get; set; } = string.Empty;
    [Required(ErrorMessage = "Customer Name is required.")]
    [MaxLength(100)] public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Enter a valid phone number.")]
    [MaxLength(20)]
    [RegularExpression(@"^[0-9+\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(150)] public string? CompanyName { get; set; }
    public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(80)] public string? State { get; set; }
    [MaxLength(30)] public string? Status { get; set; }
    public string? OwnerId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext c) { yield break; }
}

public class OpportunityDto : IValidatableObject
{
    [Required] public string OpportunityName { get; set; } = string.Empty;
    [Required] public int CustomerId { get; set; }
    public int? LeadId { get; set; }
    public string Stage { get; set; } = "Qualification";
    public string Status { get; set; } = "Open";
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? OwnerId { get; set; }
    [MaxLength(50)] public string? Source { get; set; }
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext c)
    {
        if (!Models.OpportunityTransitions.IsValidStage(Stage))
            yield return new ValidationResult("Invalid stage.", new[] { nameof(Stage) });
        var active = Status != "Won" && Status != "Lost" && Stage != "Won" && Stage != "Lost";
        if (active && Amount <= 0)
            yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });
        if (Probability < 0 || Probability > 100)
            yield return new ValidationResult("Probability must be between 0 and 100.", new[] { nameof(Probability) });
        if (active && ExpectedCloseDate.HasValue && ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}

public class LeadDto : IValidatableObject
{
    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(100)] public string LeadName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(255)] public string? Email { get; set; }
    [MaxLength(20)]
    [RegularExpression(@"^[0-9+\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    public string? Phone { get; set; }
    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(50)] public string? Source { get; set; }
    [Required(ErrorMessage = "Lead status is required.")]
    public string Status { get; set; } = "New";
    [MaxLength(20)] public string? Priority { get; set; }
    [Range(0, 1000000000, ErrorMessage = "Expected value is out of range.")]
    public decimal? ExpectedValue { get; set; }
    public string? OwnerId { get; set; }
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext c)
    {
        if (!Models.LeadTransitions.IsValidStatus(Status))
            yield return new ValidationResult("Lead status is required.", new[] { nameof(Status) });
    }
}

public class LeadConvertDto
{
    public bool CreateOpportunity { get; set; }
    public string? OpportunityName { get; set; }
    [Range(0, 1000000000, ErrorMessage = "Expected value is out of range.")]
    public decimal? Amount { get; set; }
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int? Probability { get; set; }
}

public class FollowUpDto : IValidatableObject
{
    [Required] public string RelatedType { get; set; } = "Customer";
    [Required] public int RelatedId { get; set; }
    [Required] public DateTime FollowUpDate { get; set; }
    [MaxLength(30)] public string? FollowUpType { get; set; }
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
    public string? AssignedTo { get; set; }
    public string? Remarks { get; set; }
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext c)
    {
        if (!Models.FollowUpTransitions.IsValidRelatedType(RelatedType))
            yield return new ValidationResult("Related record type is required.", new[] { nameof(RelatedType) });
        if (!Models.FollowUpTransitions.IsValidStatus(Status))
            yield return new ValidationResult("Follow-up status is required.", new[] { nameof(Status) });
        if ((Status == "Planned" || Status == "New") && FollowUpDate.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult("Follow-up date cannot be earlier than today.", new[] { nameof(FollowUpDate) });
    }
}

public class ActivityDto : IValidatableObject
{
    [Required] public string ActivityType { get; set; } = "Task";
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required] public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public string? AssignedTo { get; set; }
    public string Status { get; set; } = "Planned";

    public IEnumerable<ValidationResult> Validate(ValidationContext c)
    {
        if (!Models.ActivityTypes.IsValidType(ActivityType))
            yield return new ValidationResult("Activity type is required.", new[] { nameof(ActivityType) });
        if (!Models.ActivityTypes.IsValidStatus(Status))
            yield return new ValidationResult("Activity status is required.", new[] { nameof(Status) });
    }
}
