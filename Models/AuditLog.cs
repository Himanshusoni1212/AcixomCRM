using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

// Append-only. No UPDATE/DELETE from app code (enforced in DbContext.SaveChanges).
public class AuditLog
{
    public int AuditLogId { get; set; }

    public string? UserId { get; set; }

    [Required, MaxLength(100)] public string Action { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string EntityName { get; set; } = string.Empty;
    [MaxLength(100)] public string? Module => EntityName;

    [MaxLength(100)] public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    [MaxLength(20)] public string Result { get; set; } = "Success";
    public string? Details { get; set; }

    [MaxLength(50)] public string? IpAddress { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime Timestamp => CreatedDate;
}
