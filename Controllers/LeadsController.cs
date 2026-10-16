using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly LeadService _leads;
    private readonly UserManager<ApplicationUser> _users;

    public LeadsController(ApplicationDbContext db, AuditService audit, LeadService leads, UserManager<ApplicationUser> users)
    {
        _db = db;
        _audit = audit;
        _leads = leads;
        _users = users;
    }

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private IQueryable<Lead> Scoped()
    {
        var q = _db.Leads.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(l => l.OwnerId == Me());
        return q;
    }

    private async Task LoadSalesUsersAsync(string? selected = null)
    {
        var sales = await _users.GetUsersInRoleAsync(Roles.SalesExecutive);
        ViewBag.SalesUsers = sales.OrderBy(u => u.Email).ToList();
        ViewBag.SelectedOwner = selected;
    }

    private async Task<bool> ValidOwnerAsync(string? ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return false;
        var u = await _users.FindByIdAsync(ownerId);
        return u != null && await _users.IsInRoleAsync(u, Roles.SalesExecutive);
    }

    // GET /Leads?q=&status=&owner=&sort=name&page=1
    public async Task<IActionResult> Index(string? q, string? status, string? owner, string sort = "name", int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Scoped();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(l => l.LeadName.Contains(q) || (l.CompanyName != null && l.CompanyName.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(owner))
        {
            owner = owner.Trim();
            query = query.Where(l => _db.Users.Any(u => u.Id == l.OwnerId && (u.Email!.Contains(owner) || u.FullName.Contains(owner))));
        }

        query = sort switch
        {
            "company" => query.OrderBy(l => l.CompanyName),
            "created" => query.OrderByDescending(l => l.CreatedDate),
            _ => query.OrderBy(l => l.LeadName),
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ownerIds = items.Where(i => i.OwnerId != null).Select(i => i.OwnerId!).Distinct().ToList();
        ViewBag.OwnerEmails = await _db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id);
        ViewBag.Query = q; ViewBag.Status = status; ViewBag.Owner = owner;
        ViewBag.Sort = sort; ViewBag.Page = page; ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        if (l is null)
            return await _db.Leads.AnyAsync(x => x.LeadId == id) ? Forbid() : NotFound();
        ViewBag.OwnerEmail = l.OwnerId == null ? null : (await _db.Users.FindAsync(l.OwnerId))?.Email;
        return View(l);
    }

    public async Task<IActionResult> Create()
    {
        await LoadSalesUsersAsync(IsSales() ? Me() : null);
        return View(new Lead { Status = "New", OwnerId = IsSales() ? Me() : null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead model)
    {
        model.Status = "New"; // created leads always start New
        if (IsSales()) model.OwnerId = Me();
        else if (!await ValidOwnerAsync(model.OwnerId))
            ModelState.AddModelError(nameof(model.OwnerId), "Assigned user must be a Sales Executive.");

        if (string.IsNullOrWhiteSpace(model.LeadCode))
            model.LeadCode = await _leads.UniqueLeadCodeAsync();
        else if (await _db.Leads.AnyAsync(l => l.LeadCode == model.LeadCode))
            ModelState.AddModelError(nameof(model.LeadCode), "Lead code already exists.");
        model.CreatedDate = DateTime.UtcNow;

        if (!ModelState.IsValid) { await LoadSalesUsersAsync(model.OwnerId); return WantsJson() ? JsonValidation(400) : View(model); }

        _db.Leads.Add(model);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "Could not save the lead."); await LoadSalesUsersAsync(model.OwnerId); return WantsJson() ? JsonValidation(400) : View(model); }

        await _audit.LogAsync(Me(), "Create", "Lead", model.LeadId.ToString());
        TempData["Success"] = "Lead saved.";
        return WantsJson() ? CreatedAtAction(nameof(Details), new { id = model.LeadId }, ToResult(model)) : RedirectToAction(nameof(Details), new { id = model.LeadId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l is null) return NotFound();
        if (!RecordScope.CanAccess(l.OwnerId, User, Me())) return Forbid();
        await LoadSalesUsersAsync(l.OwnerId);
        ViewBag.Allowed = LeadTransitions.AllowedFrom(l.Status).ToList();
        return View(l);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead model)
    {
        if (id != model.LeadId) return BadRequest();
        var existing = await _db.Leads.FindAsync(id);
        if (existing is null) return NotFound();
        if (!RecordScope.CanAccess(existing.OwnerId, User, Me())) return Forbid();
        if (IsSales()) model.OwnerId = existing.OwnerId;
        else if (!await ValidOwnerAsync(model.OwnerId))
            ModelState.AddModelError(nameof(model.OwnerId), "Assigned user must be a Sales Executive.");

        var terr = LeadService.TransitionError(existing.Status, model.Status ?? existing.Status);
        if (terr != null) ModelState.AddModelError(nameof(model.Status), terr);
        if (model.LeadCode != existing.LeadCode && await _db.Leads.AnyAsync(l => l.LeadCode == model.LeadCode && l.LeadId != id))
            ModelState.AddModelError(nameof(model.LeadCode), "Lead code already exists.");

        if (!ModelState.IsValid) { await LoadSalesUsersAsync(model.OwnerId); ViewBag.Allowed = LeadTransitions.AllowedFrom(existing.Status).ToList(); return WantsJson() ? JsonValidation(400) : View(model); }

        var oldStatus = existing.Status;
        existing.LeadCode = model.LeadCode; existing.LeadName = model.LeadName;
        existing.Email = model.Email; existing.Phone = model.Phone;
        existing.CompanyName = model.CompanyName; existing.Source = model.Source;
        existing.Status = model.Status ?? existing.Status; existing.Priority = model.Priority;
        existing.ExpectedValue = model.ExpectedValue; existing.OwnerId = model.OwnerId;
        existing.Notes = model.Notes;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(Me(), "Update", "Lead", id.ToString(), "Success", $"Status {oldStatus} -> {existing.Status}");
        TempData["Success"] = "Lead saved.";
        return WantsJson() ? Ok(ToResult(existing)) : RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        if (l is null) return await _db.Leads.AnyAsync(x => x.LeadId == id) ? Forbid() : NotFound();
        return View(l);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l is null) return NotFound();
        if (!RecordScope.CanAccess(l.OwnerId, User, Me())) return Forbid();
        _db.Leads.Remove(l);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Lead", id.ToString());
        TempData["Success"] = "Lead deleted.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Convert(int id)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        if (l is null) return await _db.Leads.AnyAsync(x => x.LeadId == id) ? Forbid() : NotFound();
        if (l.Status != "Qualified")
        {
            TempData["Error"] = "Only qualified leads can be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(l);
    }

    [HttpPost, ActionName("Convert")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertConfirmed(int id, bool createOpportunity, string? opportunityName, decimal? amount, int? probability)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l is null) return NotFound();
        if (!RecordScope.CanAccess(l.OwnerId, User, Me())) return Forbid();

        var r = await _leads.ConvertAsync(id, Me(), createOpportunity, opportunityName, amount, probability);
        if (!r.Ok)
        {
            if (WantsJson())
                return StatusCode(r.ErrorCode == "CONFLICT" ? 409 : 400, new { error = new { code = r.ErrorCode, message = r.Message } });
            TempData["Error"] = r.Message;
            var scoped = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
            if (scoped is null) return Forbid();
            return View(scoped);
        }
        TempData["Success"] = "Lead converted to customer.";
        return WantsJson()
            ? StatusCode(201, new { customerId = r.Customer!.CustomerId, opportunityId = r.Opportunity?.OpportunityId })
            : RedirectToAction("Details", "Customers", new { id = r.Customer!.CustomerId });
    }

    private bool WantsJson() =>
        Request.Headers.Accept.ToString().Contains("application/json") ||
        (Request.ContentType?.Contains("application/json") ?? false);

    private IActionResult JsonValidation(int status) =>
        StatusCode(status, new { error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray()) } });

    private static object ToResult(Lead l) => new
    {
        l.LeadId, l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName,
        l.Source, l.Status, l.Priority, l.ExpectedValue, l.OwnerId, l.Notes, l.CreatedDate
    };
}
