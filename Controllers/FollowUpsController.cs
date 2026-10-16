using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public FollowUpsController(ApplicationDbContext db, AuditService audit, UserManager<ApplicationUser> users)
    {
        _db = db;
        _audit = audit;
        _users = users;
    }

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private IQueryable<FollowUp> Scoped() =>
        IsSales() ? _db.FollowUps.AsNoTracking().Where(f => f.AssignedTo == Me())
                  : _db.FollowUps.AsNoTracking();

    private async Task LoadSalesUsersAsync(string? selected = null)
    {
        ViewBag.SalesUsers = await _users.GetUsersInRoleAsync(Roles.SalesExecutive);
        ViewBag.SelectedAssignee = selected;
    }

    private async Task<bool> ValidAssigneeAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var u = await _users.FindByIdAsync(id);
        return u != null && await _users.IsInRoleAsync(u, Roles.SalesExecutive);
    }

    private async Task<Dictionary<int, string>> RelatedNamesAsync(IEnumerable<FollowUp> items)
    {
        var names = new Dictionary<int, string>();
        var groups = items.GroupBy(f => f.RelatedType);
        foreach (var g in groups)
        {
            var ids = g.Select(f => f.RelatedId).Distinct().ToList();
            if (g.Key == "Customer")
                foreach (var c in await _db.Customers.Where(x => ids.Contains(x.CustomerId)).ToListAsync())
                    foreach (var f in g.Where(x => x.RelatedId == c.CustomerId)) names[f.FollowUpId] = c.CustomerName;
            else if (g.Key == "Lead")
                foreach (var l in await _db.Leads.Where(x => ids.Contains(x.LeadId)).ToListAsync())
                    foreach (var f in g.Where(x => x.RelatedId == l.LeadId)) names[f.FollowUpId] = l.LeadName;
            else if (g.Key == "Opportunity")
                foreach (var o in await _db.Opportunities.Where(x => ids.Contains(x.OpportunityId)).ToListAsync())
                    foreach (var f in g.Where(x => x.RelatedId == o.OpportunityId)) names[f.FollowUpId] = o.OpportunityName;
        }
        return names;
    }

    private async Task<IQueryable<FollowUp>> ApplyFiltersAsync(IQueryable<FollowUp> query, DateTime? dateFrom, DateTime? dateTo, string? status, string? assignee, string? related)
    {
        if (dateFrom.HasValue) query = query.Where(f => f.FollowUpDate.Date >= dateFrom.Value.Date);
        if (dateTo.HasValue) query = query.Where(f => f.FollowUpDate.Date <= dateTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrWhiteSpace(assignee))
        {
            assignee = assignee.Trim();
            query = query.Where(f => _db.Users.Any(u => u.Id == f.AssignedTo && (u.Email!.Contains(assignee) || u.FullName.Contains(assignee))));
        }
        if (!string.IsNullOrWhiteSpace(related))
        {
            related = related.Trim();
            var custIds = await _db.Customers.Where(c => c.CustomerName.Contains(related)).Select(c => c.CustomerId).ToListAsync();
            var leadIds = await _db.Leads.Where(l => l.LeadName.Contains(related)).Select(l => l.LeadId).ToListAsync();
            var oppIds = await _db.Opportunities.Where(o => o.OpportunityName.Contains(related)).Select(o => o.OpportunityId).ToListAsync();
            query = query.Where(f =>
                (f.RelatedType == "Customer" && custIds.Contains(f.RelatedId)) ||
                (f.RelatedType == "Lead" && leadIds.Contains(f.RelatedId)) ||
                (f.RelatedType == "Opportunity" && oppIds.Contains(f.RelatedId)));
        }
        return query;
    }

    // GET /FollowUps
    public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, string? status, string? assignee, string? related, string sort = "date", int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = await ApplyFiltersAsync(Scoped(), dateFrom, dateTo, status, assignee, related);
        query = sort == "created" ? query.OrderByDescending(f => f.FollowUpId) : query.OrderBy(f => f.FollowUpDate);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.RelatedNames = await RelatedNamesAsync(items);
        var assigneeIds = items.Where(i => i.AssignedTo != null).Select(i => i.AssignedTo!).Distinct().ToList();
        ViewBag.AssigneeEmails = await _db.Users.Where(u => assigneeIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id);
        ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd"); ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
        ViewBag.Status = status; ViewBag.Assignee = assignee; ViewBag.Related = related;
        ViewBag.Sort = sort; ViewBag.Page = page; ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    public async Task<IActionResult> Upcoming()
    {
        var today = DateTime.UtcNow.Date;
        var items = await Scoped().Where(f => f.Status == "Planned" && f.FollowUpDate.Date >= today).OrderBy(f => f.FollowUpDate).Take(100).ToListAsync();
        ViewBag.RelatedNames = await RelatedNamesAsync(items);
        return View(items);
    }

    public async Task<IActionResult> Overdue()
    {
        var today = DateTime.UtcNow.Date;
        var items = await Scoped().Where(f => f.Status == "Planned" && f.FollowUpDate.Date < today).OrderBy(f => f.FollowUpDate).Take(100).ToListAsync();
        ViewBag.RelatedNames = await RelatedNamesAsync(items);
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var f = await Scoped().FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f is null) return await _db.FollowUps.AnyAsync(x => x.FollowUpId == id) ? Forbid() : NotFound();
        var r = await RelatedRecords.ResolveAsync(_db, f.RelatedType, f.RelatedId, false, Me());
        ViewBag.RelatedName = r.Found ? r.Name : $"{f.RelatedType} #{f.RelatedId}";
        ViewBag.AssigneeEmail = f.AssignedTo == null ? null : (await _db.Users.FindAsync(f.AssignedTo))?.Email;
        return View(f);
    }

    public async Task<IActionResult> Create()
    {
        await LoadSalesUsersAsync(IsSales() ? Me() : null);
        return View(new FollowUp { Status = "Planned", FollowUpDate = DateTime.UtcNow.Date, AssignedTo = IsSales() ? Me() : null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp model)
    {
        if (IsSales()) model.AssignedTo = Me();
        else if (!await ValidAssigneeAsync(model.AssignedTo))
            ModelState.AddModelError(nameof(model.AssignedTo), "Assigned user must be a Sales Executive.");

        var r = await RelatedRecords.ResolveAsync(_db, model.RelatedType, model.RelatedId, IsSales(), Me());
        if (!r.Found)
            ModelState.AddModelError(nameof(model.RelatedId), "Related Customer, Lead or Opportunity not found or access denied.");
        else { model.CustomerId = r.CustomerId; model.LeadId = r.LeadId; }

        if (!ModelState.IsValid) { await LoadSalesUsersAsync(model.AssignedTo); return WantsJson() ? JsonValidation(400) : View(model); }

        _db.FollowUps.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "FollowUp", model.FollowUpId.ToString(), "Success", $"{model.RelatedType} {model.RelatedId}");
        TempData["Success"] = "Follow-up saved.";
        return WantsJson() ? CreatedAtAction(nameof(Details), new { id = model.FollowUpId }, ToResult(model)) : RedirectToAction(nameof(Details), new { id = model.FollowUpId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound();
        if (IsSales() && f.AssignedTo != Me()) return Forbid();
        await LoadSalesUsersAsync(f.AssignedTo);
        return View(f);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUp model)
    {
        if (id != model.FollowUpId) return BadRequest();
        var existing = await _db.FollowUps.FindAsync(id);
        if (existing is null) return NotFound();
        if (IsSales() && existing.AssignedTo != Me()) return Forbid();
        if (IsSales()) model.AssignedTo = existing.AssignedTo;
        else if (!await ValidAssigneeAsync(model.AssignedTo))
            ModelState.AddModelError(nameof(model.AssignedTo), "Assigned user must be a Sales Executive.");

        var terr = FollowUpTransitions.TransitionError(existing.Status, model.Status ?? existing.Status);
        if (terr != null) ModelState.AddModelError(nameof(model.Status), terr);

        var r = await RelatedRecords.ResolveAsync(_db, model.RelatedType, model.RelatedId, IsSales(), Me());
        if (!r.Found)
            ModelState.AddModelError(nameof(model.RelatedId), "Related Customer, Lead or Opportunity not found or access denied.");

        if (!ModelState.IsValid) { await LoadSalesUsersAsync(model.AssignedTo); return WantsJson() ? JsonValidation(400) : View(model); }

        var rescheduled = existing.FollowUpDate != model.FollowUpDate;
        var oldStatus = existing.Status;
        existing.RelatedType = model.RelatedType; existing.RelatedId = model.RelatedId;
        existing.CustomerId = r.CustomerId; existing.LeadId = r.LeadId;
        existing.FollowUpDate = model.FollowUpDate; existing.FollowUpType = model.FollowUpType;
        existing.Subject = model.Subject; existing.Status = model.Status ?? existing.Status;
        existing.AssignedTo = model.AssignedTo; existing.Remarks = model.Remarks; existing.Notes = model.Notes;
        await _db.SaveChangesAsync();

        if (rescheduled)
            await _audit.LogAsync(Me(), "Reschedule", "FollowUp", id.ToString(), "Success", $"Date {existing.FollowUpDate:yyyy-MM-dd}");
        await _audit.LogAsync(Me(), "Update", "FollowUp", id.ToString(), "Success", $"Status {oldStatus} -> {existing.Status}");
        TempData["Success"] = rescheduled ? "Follow-up rescheduled." : "Follow-up saved.";
        return WantsJson() ? Ok(ToResult(existing)) : RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound();
        if (IsSales() && f.AssignedTo != Me()) return Forbid();
        if (f.Status != "Planned")
        {
            TempData["Error"] = $"Only planned follow-ups can be completed.";
            return RedirectToAction(nameof(Details), new { id });
        }
        f.Status = "Completed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Complete", "FollowUp", id.ToString(), "Success", $"{f.RelatedType} {f.RelatedId}");
        TempData["Success"] = "Follow-up completed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Missed(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound();
        if (IsSales() && f.AssignedTo != Me()) return Forbid();
        if (f.Status != "Planned")
        {
            TempData["Error"] = "Only planned follow-ups can be marked missed.";
            return RedirectToAction(nameof(Details), new { id });
        }
        f.Status = "Missed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Update", "FollowUp", id.ToString(), "Success", "Status Planned -> Missed");
        TempData["Success"] = "Follow-up marked missed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var f = await Scoped().FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f is null) return await _db.FollowUps.AnyAsync(x => x.FollowUpId == id) ? Forbid() : NotFound();
        return View(f);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound();
        if (IsSales() && f.AssignedTo != Me()) return Forbid();
        _db.FollowUps.Remove(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "FollowUp", id.ToString());
        TempData["Success"] = "Follow-up deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool WantsJson() =>
        Request.Headers.Accept.ToString().Contains("application/json") ||
        (Request.ContentType?.Contains("application/json") ?? false);

    private IActionResult JsonValidation(int status) =>
        StatusCode(status, new { error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray()) } });

    private static object ToResult(FollowUp f) => new
    {
        f.FollowUpId, f.RelatedType, f.RelatedId, f.CustomerId, f.LeadId,
        f.FollowUpDate, f.FollowUpType, f.Subject, f.Status, f.AssignedTo, f.Remarks, f.Notes
    };
}
