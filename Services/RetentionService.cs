using AcxiomCRM.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AcxiomCRM.Services;

// Enforces the published retention schedule (Privacy Policy: audit logs 1 to 3 years).
// Config: Retention:AuditLogDays (env Retention__AuditLogDays), default 1095. Zero disables.
public static class RetentionService
{
    public static async Task PurgeAsync(IServiceProvider sp)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Retention");
        var days = config.GetValue<int?>("Retention:AuditLogDays") ?? 1095;
        if (days <= 0)
        {
            log.LogInformation("Audit retention purge disabled (Retention:AuditLogDays={Days}).", days);
            return;
        }

        var db = sp.GetRequiredService<ApplicationDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var stale = await db.AuditLogs.Where(a => a.CreatedDate < cutoff).ToListAsync();
        if (stale.Count == 0) return;
        // Bypass append-only guard intent: retention deletion is the published policy, not app editing.
        db.AllowRetentionPurge = true;
        db.AuditLogs.RemoveRange(stale);
        await db.SaveChangesAsync();
        db.AllowRetentionPurge = false;
        log.LogInformation("Retention purge deleted {Count} audit rows older than {Cutoff:u}.", stale.Count, cutoff);
    }
}
