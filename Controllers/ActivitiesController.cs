using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public ActivitiesController(ApplicationDbContext db, AuditService audit, UserManager<ApplicationUser> users)
    {
        _db = db;
        _audit = audit;
        _users = users;
    }

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private IQueryable<Activity> Scoped() =>
        IsSales() ? _db.Activities.AsNoTracking().Where(a => a.AssignedTo == Me())
                  : _db.Activities.AsNoTracking();

    private async Task LoadListsAsync(int? customerId = null, int? leadId = null, string? assignee = null)
    {
        ViewBag.Customers = await (IsSales() ? _db.Customers.Where(c => c.OwnerId == Me()) : _db.Customers).OrderBy(c => c.CustomerName).ToListAsync();
        ViewBag.Leads = await (IsSales() ? _db.Leads.Where(l => l.OwnerId == Me()) : _db.Leads).OrderBy(l => l.LeadName).ToListAsync();
        ViewBag.SalesUsers = await _users.GetUsersInRoleAsync(Roles.SalesExecutive);
        ViewBag.SelectedCustomer = customerId; ViewBag.SelectedLead = leadId; ViewBag.SelectedAssignee = assignee;
    }

    private async Task<bool> ValidAssigneeAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var u = await _users.FindByIdAsync(id);
        return u != null && await _users.IsInRoleAsync(u, Roles.SalesExecutive);
    }

    private async Task<bool> VisibleCustomerAsync(int? id) =>
        !id.HasValue || await (IsSales() ? _db.Customers.Where(c => c.OwnerId == Me()) : _db.Customers).AnyAsync(c => c.CustomerId == id);

    private async Task<bool> VisibleLeadAsync(int? id) =>
        !id.HasValue || await (IsSales() ? _db.Leads.Where(l => l.OwnerId == Me()) : _db.Leads).AnyAsync(l => l.LeadId == id);

    // GET /Activities?type=&dateFrom=&dateTo=&status=&assignee=&sort=date&page=1
    public async Task<IActionResult> Index(string? type, DateTime? dateFrom, DateTime? dateTo, string? status, string? assignee, string sort = "date", int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Scoped();
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(a => a.ActivityType == type);
        if (dateFrom.HasValue) query = query.Where(a => a.ActivityDate.Date >= dateFrom.Value.Date);
        if (dateTo.HasValue) query = query.Where(a => a.ActivityDate.Date <= dateTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(assignee))
        {
            assignee = assignee.Trim();
            query = query.Where(a => _db.Users.Any(u => u.Id == a.AssignedTo && (u.Email!.Contains(assignee) || u.FullName.Contains(assignee))));
        }
        query = sort == "created" ? query.OrderByDescending(a => a.ActivityId) : query.OrderByDescending(a => a.ActivityDate);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Type = type;
        ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd"); ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
        ViewBag.Status = status; ViewBag.Assignee = assignee; ViewBag.Sort = sort;
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var a = await Scoped().FirstOrDefaultAsync(x => x.ActivityId == id);
        if (a is null) return await _db.Activities.AnyAsync(x => x.ActivityId == id) ? Forbid() : NotFound();
        ViewBag.CustomerName = a.CustomerId == null ? null : (await _db.Customers.FindAsync(a.CustomerId))?.CustomerName;
        ViewBag.LeadName = a.LeadId == null ? null : (await _db.Leads.FindAsync(a.LeadId))?.LeadName;
        ViewBag.AssigneeEmail = a.AssignedTo == null ? null : (await _db.Users.FindAsync(a.AssignedTo))?.Email;
        return View(a);
    }

    public async Task<IActionResult> Create()
    {
        await LoadListsAsync(null, null, IsSales() ? Me() : null);
        return View(new Activity { ActivityDate = DateTime.UtcNow.Date, AssignedTo = IsSales() ? Me() : null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity model)
    {
        if (IsSales()) model.AssignedTo = Me();
        else if (!await ValidAssigneeAsync(model.AssignedTo))
            ModelState.AddModelError(nameof(model.AssignedTo), "Assigned user must be a Sales Executive.");
        if (!await VisibleCustomerAsync(model.CustomerId))
            ModelState.AddModelError(nameof(model.CustomerId), "Customer not found or access denied.");
        if (!await VisibleLeadAsync(model.LeadId))
            ModelState.AddModelError(nameof(model.LeadId), "Lead not found or access denied.");

        if (!ModelState.IsValid) { await LoadListsAsync(model.CustomerId, model.LeadId, model.AssignedTo); return WantsJson() ? JsonValidation(400) : View(model); }

        _db.Activities.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "Activity", model.ActivityId.ToString());
        TempData["Success"] = "Activity saved.";
        return WantsJson() ? CreatedAtAction(nameof(Details), new { id = model.ActivityId }, ToResult(model)) : RedirectToAction(nameof(Details), new { id = model.ActivityId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a is null) return NotFound();
        if (IsSales() && a.AssignedTo != Me()) return Forbid();
        await LoadListsAsync(a.CustomerId, a.LeadId, a.AssignedTo);
        return View(a);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity model)
    {
        if (id != model.ActivityId) return BadRequest();
        var existing = await _db.Activities.FindAsync(id);
        if (existing is null) return NotFound();
        if (IsSales() && existing.AssignedTo != Me()) return Forbid();
        if (IsSales()) model.AssignedTo = existing.AssignedTo;
        else if (!await ValidAssigneeAsync(model.AssignedTo))
            ModelState.AddModelError(nameof(model.AssignedTo), "Assigned user must be a Sales Executive.");
        if (!await VisibleCustomerAsync(model.CustomerId))
            ModelState.AddModelError(nameof(model.CustomerId), "Customer not found or access denied.");
        if (!await VisibleLeadAsync(model.LeadId))
            ModelState.AddModelError(nameof(model.LeadId), "Lead not found or access denied.");

        if (!ModelState.IsValid) { await LoadListsAsync(model.CustomerId, model.LeadId, model.AssignedTo); return WantsJson() ? JsonValidation(400) : View(model); }

        existing.ActivityType = model.ActivityType; existing.Subject = model.Subject;
        existing.Description = model.Description; existing.ActivityDate = model.ActivityDate;
        existing.CustomerId = model.CustomerId; existing.LeadId = model.LeadId;
        existing.AssignedTo = model.AssignedTo; existing.Status = model.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Update", "Activity", id.ToString());
        TempData["Success"] = "Activity saved.";
        return WantsJson() ? Ok(ToResult(existing)) : RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var a = await Scoped().FirstOrDefaultAsync(x => x.ActivityId == id);
        if (a is null) return await _db.Activities.AnyAsync(x => x.ActivityId == id) ? Forbid() : NotFound();
        return View(a);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a is null) return NotFound();
        if (IsSales() && a.AssignedTo != Me()) return Forbid();
        _db.Activities.Remove(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Activity", id.ToString());
        TempData["Success"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool WantsJson() =>
        Request.Headers.Accept.ToString().Contains("application/json") ||
        (Request.ContentType?.Contains("application/json") ?? false);

    private IActionResult JsonValidation(int status) =>
        StatusCode(status, new { error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray()) } });

    private static object ToResult(Activity a) => new
    {
        a.ActivityId, a.ActivityType, a.Subject, a.Description, a.ActivityDate,
        a.CustomerId, a.LeadId, a.AssignedTo, a.Status
    };
}
