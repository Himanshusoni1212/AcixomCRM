using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string userId, string action, string entity, string? recordId,
        string result = "Success", string? details = null, string? oldVal = null, string? newVal = null)
    {
        var ip = _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, EntityName = entity, RecordId = recordId,
            Result = result, Details = details, OldValue = oldVal, NewValue = newVal, IpAddress = ip,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
