using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

// Admin full, Manager limited team view, SalesExecutive denied (403).
[Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AuditLogsController(ApplicationDbContext db) => _db = db;

    // NOTE: filter is named auditAction (not action) because {action} is a route value.
    public async Task<IActionResult> Index(string? user, string? module, string? auditAction, DateTime? from, DateTime? to, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(a => a.UserId == user);
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(a => a.EntityName == module);
        if (!string.IsNullOrWhiteSpace(auditAction)) q = q.Where(a => a.Action == auditAction);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.CreatedDate <= to.Value.Date.AddDays(1).AddTicks(-1));
        q = q.OrderByDescending(a => a.CreatedDate);

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
