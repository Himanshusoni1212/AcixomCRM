using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Customer
{
    public int CustomerId { get; set; }

    [Required, MaxLength(20)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer Name is required.")]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a valid phone number.")]
    [MaxLength(20)]
    [RegularExpression(@"^[0-9+\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(80)] public string? State { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Active";

    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }
    public string? CreatedBy { get; set; }
}
