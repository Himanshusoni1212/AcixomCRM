using System.Text;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

// Reports hub + 8 reports. Sales sees only assigned records.
// Audit report is Admin-only; Sales/Conversion and User Activity are Admin/Manager.
[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db) => _db = db;

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private static FileContentResult CsvFile(string name, string[][] rows)
    {
        var sb = new StringBuilder();
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", r.Select(c => "\"" + (c ?? "").Replace("\"", "\"\"") + "\"")));
        return new FileContentResult(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv")
        {
            FileDownloadName = $"{name}-{DateTime.UtcNow:yyyyMMdd}.csv"
        };
    }

    private async Task<Dictionary<string, string>> OwnerEmailsAsync(IEnumerable<string?> ids)
    {
        var list = ids.Where(i => i != null).Cast<string>().Distinct().ToList();
        return await _db.Users.Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id);
    }

    public IActionResult Index() => View();

    // ---- Customer report: list, status, owner, created date ----
    public async Task<IActionResult> Customers(string? status, string? sort = "name", int page = 1, int pageSize = 20, string? export = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Customers.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(c => c.OwnerId == Me());
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        q = sort == "created" ? q.OrderByDescending(c => c.CreatedDate) : q.OrderBy(c => c.CustomerName);

        if (export == "csv")
        {
            var all = await q.Take(2000).ToListAsync();
            var emails = await OwnerEmailsAsync(all.Select(c => c.OwnerId));
            var rows = new List<string[]> { new[] { "Name", "Email", "Phone", "Company", "Status", "Owner", "Created" } };
            rows.AddRange(all.Select(c => new[]
            {
                c.CustomerName, c.Email, c.Phone, c.CompanyName ?? "",
                c.Status, c.OwnerId != null && emails.TryGetValue(c.OwnerId, out var e) ? e : "",
                c.CreatedDate.ToString("u")
            }));
            return CsvFile("customer-report", rows.ToArray());
        }

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.OwnerEmails = await OwnerEmailsAsync(items.Select(c => c.OwnerId));
        ViewBag.Status = status; ViewBag.Sort = sort; ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    // ---- Lead report: source, status, owner, conversion ----
    public async Task<IActionResult> Leads(string? status, string? source, string sort = "name", int page = 1, int pageSize = 20, string? export = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Leads.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(l => l.OwnerId == Me());
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(l => l.Source == source);
        q = sort == "created" ? q.OrderByDescending(l => l.CreatedDate) : q.OrderBy(l => l.LeadName);

        if (export == "csv")
        {
            var all = await q.Take(2000).ToListAsync();
            var emails = await OwnerEmailsAsync(all.Select(l => l.OwnerId));
            var rows = new List<string[]> { new[] { "Name", "Company", "Source", "Status", "Converted", "Owner", "Created" } };
            rows.AddRange(all.Select(l => new[]
            {
                l.LeadName, l.CompanyName ?? "", l.Source ?? "", l.Status,
                l.Status == "Converted" ? "Yes" : "No",
                l.OwnerId != null && emails.TryGetValue(l.OwnerId, out var e) ? e : "",
                l.CreatedDate.ToString("u")
            }));
            return CsvFile("lead-report", rows.ToArray());
        }

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.OwnerEmails = await OwnerEmailsAsync(items.Select(l => l.OwnerId));
        ViewBag.Status = status; ViewBag.Source = source; ViewBag.Sort = sort; ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    // ---- Follow-up report: planned/completed/missed/overdue, date, owner ----
    public async Task<IActionResult> FollowUps(string? status, DateTime? from, DateTime? to, string sort = "date", int page = 1, int pageSize = 20, string? export = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.FollowUps.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(f => f.AssignedTo == Me());
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(f => f.Status == status);
        if (from.HasValue) q = q.Where(f => f.FollowUpDate.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(f => f.FollowUpDate.Date <= to.Value.Date);
        q = sort == "created" ? q.OrderByDescending(f => f.FollowUpId) : q.OrderBy(f => f.FollowUpDate);

        if (export == "csv")
        {
            var all = await q.Take(2000).ToListAsync();
            var emails = await OwnerEmailsAsync(all.Select(f => f.AssignedTo));
            var rows = new List<string[]> { new[] { "Date", "Subject", "Related", "Status", "Overdue", "Assignee" } };
            rows.AddRange(all.Select(f => new[]
            {
                f.FollowUpDate.ToString("yyyy-MM-dd"), f.Subject, $"{f.RelatedType} {f.RelatedId}", f.Status,
                f.Status == "Planned" && f.FollowUpDate.Date < DateTime.UtcNow.Date ? "Yes" : "No",
                f.AssignedTo != null && emails.TryGetValue(f.AssignedTo, out var e) ? e : ""
            }));
            return CsvFile("followup-report", rows.ToArray());
        }

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.AssigneeEmails = await OwnerEmailsAsync(items.Select(f => f.AssignedTo));
        ViewBag.Status = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Sort = sort; ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    // ---- Opportunity report: stage, amount, probability, close date ----
    public async Task<IActionResult> Opportunities(string? stage, string? status, string sort = "name", int page = 1, int pageSize = 20, string? export = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Opportunities.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(o => o.OwnerId == Me());
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        q = sort == "created" ? q.OrderByDescending(o => o.CreatedDate) : q.OrderBy(o => o.OpportunityName);

        if (export == "csv")
        {
            var all = await q.Take(2000).ToListAsync();
            var rows = new List<string[]> { new[] { "Name", "Stage", "Amount", "Probability", "Weighted", "CloseDate", "Status" } };
            rows.AddRange(all.Select(o => new[]
            {
                o.OpportunityName, o.Stage, o.Amount.ToString("N2"), o.Probability.ToString(),
                (o.Amount * o.Probability / 100).ToString("N2"),
                o.ExpectedCloseDate?.ToString("yyyy-MM-dd") ?? "", o.Status
            }));
            return CsvFile("opportunity-report", rows.ToArray());
        }

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Stage = stage; ViewBag.Status = status; ViewBag.Sort = sort; ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    // ---- Pipeline report: stage-wise + owner-wise + weighted ----
    public async Task<IActionResult> Pipeline(string? export = null)
    {
        var q = _db.Opportunities.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(o => o.OwnerId == Me());
        var all = await q.ToListAsync();
        var emails = await OwnerEmailsAsync(all.Select(o => o.OwnerId));

        var byStage = OpportunityTransitions.Stages
            .Select(s => new
            {
                Stage = s,
                Count = all.Count(o => o.Stage == s),
                Amount = all.Where(o => o.Stage == s).Sum(o => o.Amount),
                Weighted = all.Where(o => o.Stage == s).Sum(o => o.Amount * o.Probability / 100)
            }).ToList();
        var byOwner = all.GroupBy(o => o.OwnerId).Select(g => new
        {
            Owner = g.Key != null && emails.TryGetValue(g.Key, out var e) ? e : "(unassigned)",
            Count = g.Count(),
            Amount = g.Sum(o => o.Amount),
            Weighted = g.Sum(o => o.Amount * o.Probability / 100)
        }).ToList();

        if (export == "csv")
        {
            var rows = new List<string[]> { new[] { "Section", "Key", "Count", "Amount", "Weighted" } };
            rows.AddRange(byStage.Select(s => new[] { "Stage", s.Stage, s.Count.ToString(), s.Amount.ToString("N2"), s.Weighted.ToString("N2") }));
            rows.AddRange(byOwner.Select(s => new[] { "Owner", s.Owner, s.Count.ToString(), s.Amount.ToString("N2"), s.Weighted.ToString("N2") }));
            return CsvFile("pipeline-report", rows.ToArray());
        }

        ViewBag.ByStage = byStage.Cast<object>().ToList();
        ViewBag.ByOwner = byOwner.Cast<object>().ToList();
        return View();
    }

    // ---- Sales/Conversion report: lead conversion + opportunity outcomes ----
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> Sales(string? export = null)
    {
        var leads = await _db.Leads.AsNoTracking().ToListAsync();
        var opps = await _db.Opportunities.AsNoTracking().ToListAsync();
        var leadByStatus = leads.GroupBy(l => l.Status).ToDictionary(g => g.Key, g => g.Count());
        var converted = leadByStatus.TryGetValue("Converted", out var c) ? c : 0;
        ViewBag.LeadByStatus = leadByStatus;
        ViewBag.LeadConversionRate = leads.Count == 0 ? 0 : Math.Round(100.0 * converted / leads.Count, 1);
        var won = opps.Count(o => o.Stage == "Won");
        var lost = opps.Count(o => o.Stage == "Lost");
        ViewBag.Won = won; ViewBag.Lost = lost;
        ViewBag.Open = opps.Count - won - lost;
        ViewBag.WinRate = won + lost == 0 ? 0 : Math.Round(100.0 * won / (won + lost), 1);
        ViewBag.WonAmount = opps.Where(o => o.Stage == "Won").Sum(o => o.Amount);

        if (export == "csv")
        {
            var rows = new List<string[]>
            {
                new string[] { "Metric", "Value" },
                new string[] { "Total leads", leads.Count.ToString() },
                new string[] { "Converted leads", converted.ToString() },
                new string[] { "Lead conversion %", $"{ViewBag.LeadConversionRate}" },
                new string[] { "Won opportunities", won.ToString() },
                new string[] { "Lost opportunities", lost.ToString() },
                new string[] { "Win rate %", $"{ViewBag.WinRate}" },
                new string[] { "Won amount", ((decimal)ViewBag.WonAmount).ToString("N2") },
            };
            foreach (var kv in leadByStatus) rows.Add(new[] { $"Leads: {kv.Key}", kv.Value.ToString() });
            return CsvFile("sales-report", rows.ToArray());
        }
        return View();
    }

    // ---- User activity report: actions + counts ----
    // NOTE: auditAction (not action) because {action} is a route value.
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> UserActivity(string? auditAction, DateTime? from, DateTime? to, string? export = null)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(auditAction)) q = q.Where(a => a.Action == auditAction);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.CreatedDate <= to.Value.Date.AddDays(1).AddTicks(-1));
        var groups = await q.GroupBy(a => new { a.UserId, a.Action })
            .Select(g => new { g.Key.UserId, g.Key.Action, Count = g.Count() })
            .OrderByDescending(g => g.Count).Take(500).ToListAsync();
        var emails = await OwnerEmailsAsync(groups.Select(g => g.UserId));
        var rows = groups.Select(g => new
        {
            User = g.UserId != null && emails.TryGetValue(g.UserId, out var e) ? e : "(system)",
            g.Action,
            g.Count
        }).ToList();

        if (export == "csv")
        {
            var csv = new List<string[]> { new[] { "User", "Action", "Count" } };
            csv.AddRange(rows.Select(r => new[] { r.User, r.Action, r.Count.ToString() }));
            return CsvFile("user-activity-report", csv.ToArray());
        }

        ViewBag.Action = auditAction;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(rows.Cast<object>().ToList());
    }

    // ---- Audit report: Admin only ----
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Audit(string? user, string? module, string? auditAction, DateTime? from, DateTime? to, int page = 1, int pageSize = 20, string? export = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(a => a.UserId == user);
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(a => a.EntityName == module);
        if (!string.IsNullOrWhiteSpace(auditAction)) q = q.Where(a => a.Action == auditAction);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.CreatedDate <= to.Value.Date.AddDays(1).AddTicks(-1));
        q = q.OrderByDescending(a => a.CreatedDate);

        if (export == "csv")
        {
            var all = await q.Take(2000).ToListAsync();
            var rows = new List<string[]> { new[] { "Time", "User", "Action", "Module", "Record", "Result", "IP" } };
            rows.AddRange(all.Select(a => new[]
            {
                a.CreatedDate.ToString("u"), a.UserId ?? "", a.Action, a.EntityName,
                a.RecordId ?? "", a.Result, a.IpAddress ?? ""
            }));
            return CsvFile("audit-report", rows.ToArray());
        }

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.User = user; ViewBag.Module = module; ViewBag.Action = auditAction;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }
}
