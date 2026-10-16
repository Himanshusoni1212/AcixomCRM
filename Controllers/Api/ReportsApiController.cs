using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

// JSON twin of the MVC reports. Same role gates and sales scoping.
// Names: customers, leads, followups, opportunities, pipeline, sales, user-activity, audit.
[Route("api/reports")]
[ApiController]
[Authorize]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ReportsApiController(ApplicationDbContext db) => _db = db;

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);
    private bool IsManagerOrAdmin() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Manager);

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, [FromQuery] string? status, [FromQuery] string? stage,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return name.ToLowerInvariant() switch
        {
            "customers" => Ok(await CustomerReport(status, page, pageSize)),
            "leads" => Ok(await LeadReport(status, page, pageSize)),
            "followups" => Ok(await FollowUpReport(status, page, pageSize)),
            "opportunities" => Ok(await OpportunityReport(stage, status, page, pageSize)),
            "pipeline" => Ok(await PipelineReport()),
            "sales" => IsManagerOrAdmin() ? Ok(await SalesReport()) : StatusCode(403, Err("FORBIDDEN", "Access denied.")),
            "user-activity" => IsManagerOrAdmin() ? Ok(await UserActivityReport()) : StatusCode(403, Err("FORBIDDEN", "Access denied.")),
            "audit" => User.IsInRole(Roles.Admin) ? Ok(await AuditReport(page, pageSize)) : StatusCode(403, Err("FORBIDDEN", "Access denied.")),
            _ => NotFound(Err("NOT_FOUND", $"Unknown report '{name}'."))
        };
    }

    private async Task<object> CustomerReport(string? status, int page, int pageSize)
    {
        var q = _db.Customers.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(c => c.OwnerId == Me());
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        q = q.OrderBy(c => c.CustomerName);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var emails = await OwnerEmailsAsync(items.Select(c => c.OwnerId));
        return new
        {
            total, page, pageSize,
            items = items.Select(c => new
            {
                c.CustomerId, c.CustomerName, c.Email, c.Phone, c.CompanyName, c.Status,
                owner = c.OwnerId != null && emails.TryGetValue(c.OwnerId, out var e) ? e : (string?)null,
                c.CreatedDate
            })
        };
    }

    private async Task<object> LeadReport(string? status, int page, int pageSize)
    {
        var q = _db.Leads.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(l => l.OwnerId == Me());
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        q = q.OrderBy(l => l.LeadName);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var emails = await OwnerEmailsAsync(items.Select(l => l.OwnerId));
        return new
        {
            total, page, pageSize,
            items = items.Select(l => new
            {
                l.LeadId, l.LeadName, l.CompanyName, l.Source, l.Status,
                converted = l.Status == "Converted",
                owner = l.OwnerId != null && emails.TryGetValue(l.OwnerId, out var e) ? e : (string?)null,
                l.CreatedDate
            })
        };
    }

    private async Task<object> FollowUpReport(string? status, int page, int pageSize)
    {
        var q = _db.FollowUps.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(f => f.AssignedTo == Me());
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(f => f.Status == status);
        q = q.OrderBy(f => f.FollowUpDate);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new
        {
            total, page, pageSize,
            items = items.Select(f => new
            {
                f.FollowUpId, f.FollowUpDate, f.Subject, f.RelatedType, f.RelatedId,
                f.Status,
                overdue = f.Status == "Planned" && f.FollowUpDate.Date < DateTime.UtcNow.Date,
                f.AssignedTo
            })
        };
    }

    private async Task<object> OpportunityReport(string? stage, string? status, int page, int pageSize)
    {
        var q = _db.Opportunities.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(o => o.OwnerId == Me());
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        q = q.OrderBy(o => o.OpportunityName);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new
        {
            total, page, pageSize,
            items = items.Select(o => new
            {
                o.OpportunityId, o.OpportunityName, o.Stage, o.Amount, o.Probability,
                weighted = o.Amount * o.Probability / 100, o.ExpectedCloseDate, o.Status
            })
        };
    }

    private async Task<object> PipelineReport()
    {
        var all = await (IsSales()
            ? _db.Opportunities.AsNoTracking().Where(o => o.OwnerId == Me()).ToListAsync()
            : _db.Opportunities.AsNoTracking().ToListAsync());
        var emails = await OwnerEmailsAsync(all.Select(o => o.OwnerId));
        return new
        {
            byStage = OpportunityTransitions.Stages.Select(s => new
            {
                stage = s,
                count = all.Count(o => o.Stage == s),
                amount = all.Where(o => o.Stage == s).Sum(o => o.Amount),
                weighted = all.Where(o => o.Stage == s).Sum(o => o.Amount * o.Probability / 100)
            }),
            byOwner = all.GroupBy(o => o.OwnerId).Select(g => new
            {
                owner = g.Key != null && emails.TryGetValue(g.Key, out var e) ? e : "(unassigned)",
                count = g.Count(),
                amount = g.Sum(o => o.Amount),
                weighted = g.Sum(o => o.Amount * o.Probability / 100)
            })
        };
    }

    private async Task<object> SalesReport()
    {
        var leads = await _db.Leads.AsNoTracking().ToListAsync();
        var opps = await _db.Opportunities.AsNoTracking().ToListAsync();
        var converted = leads.Count(l => l.Status == "Converted");
        var won = opps.Count(o => o.Stage == "Won");
        var lost = opps.Count(o => o.Stage == "Lost");
        return new
        {
            totalLeads = leads.Count,
            convertedLeads = converted,
            leadConversionRate = leads.Count == 0 ? 0 : Math.Round(100.0 * converted / leads.Count, 1),
            wonOpportunities = won,
            lostOpportunities = lost,
            openOpportunities = opps.Count - won - lost,
            winRate = won + lost == 0 ? 0 : Math.Round(100.0 * won / (won + lost), 1),
            wonAmount = opps.Where(o => o.Stage == "Won").Sum(o => o.Amount)
        };
    }

    private async Task<object> UserActivityReport()
    {
        var groups = await _db.AuditLogs.AsNoTracking()
            .GroupBy(a => new { a.UserId, a.Action })
            .Select(g => new { g.Key.UserId, g.Key.Action, Count = g.Count() })
            .OrderByDescending(g => g.Count).Take(500).ToListAsync();
        var emails = await OwnerEmailsAsync(groups.Select(g => g.UserId));
        return new
        {
            items = groups.Select(g => new
            {
                user = g.UserId != null && emails.TryGetValue(g.UserId, out var e) ? e : "(system)",
                g.Action,
                g.Count
            })
        };
    }

    private async Task<object> AuditReport(int page, int pageSize)
    {
        var q = _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedDate);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new
        {
            total, page, pageSize,
            items = items.Select(a => new
            {
                a.AuditLogId, a.UserId, a.Action, module = a.EntityName,
                recordId = a.RecordId, a.Result, a.IpAddress, a.CreatedDate
            })
        };
    }

    private async Task<Dictionary<string, string>> OwnerEmailsAsync(IEnumerable<string?> ids)
    {
        var list = ids.Where(i => i != null).Cast<string>().Distinct().ToList();
        return await _db.Users.Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id);
    }

    private static object Err(string code, string message) => new { error = new { code, message } };
}
