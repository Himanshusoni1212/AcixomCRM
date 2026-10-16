using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public OpportunitiesController(ApplicationDbContext db, AuditService audit, UserManager<ApplicationUser> users)
    {
        _db = db;
        _audit = audit;
        _users = users;
    }

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private IQueryable<Opportunity> Scoped() =>
        IsSales() ? _db.Opportunities.AsNoTracking().Where(o => o.OwnerId == Me())
                  : _db.Opportunities.AsNoTracking();

    private IQueryable<Customer> ScopedCustomers() =>
        IsSales() ? _db.Customers.AsNoTracking().Where(c => c.OwnerId == Me())
                  : _db.Customers.AsNoTracking();

    private async Task LoadListsAsync(int? customerId = null, string? ownerId = null)
    {
        ViewBag.Customers = await ScopedCustomers().OrderBy(c => c.CustomerName).ToListAsync();
        ViewBag.SalesUsers = await _users.GetUsersInRoleAsync(Roles.SalesExecutive);
        ViewBag.SelectedCustomer = customerId;
        ViewBag.SelectedOwner = ownerId;
    }

    private async Task<bool> ValidOwnerAsync(string? ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return false;
        var u = await _users.FindByIdAsync(ownerId);
        return u != null && await _users.IsInRoleAsync(u, Roles.SalesExecutive);
    }

    private async Task<Customer?> VisibleCustomerAsync(int customerId) =>
        await ScopedCustomers().FirstOrDefaultAsync(c => c.CustomerId == customerId);

    // GET /Opportunities?q=&customer=&stage=&status=&sort=name&page=1
    public async Task<IActionResult> Index(string? q, string? customer, string? stage, string? status, string sort = "name", int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Scoped();

        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(o => o.OpportunityName.Contains(q)); }
        if (!string.IsNullOrWhiteSpace(customer))
        {
            customer = customer.Trim();
            query = query.Where(o => _db.Customers.Any(c => c.CustomerId == o.CustomerId && c.CustomerName.Contains(customer)));
        }
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);

        query = sort switch
        {
            "created" => query.OrderByDescending(o => o.CreatedDate),
            _ => query.OrderBy(o => o.OpportunityName),
        };

        var total = await query.CountAsync();
        List<Opportunity> items;
        if (sort == "amount")
        {
            // SQLite cannot ORDER BY decimal; sort open pipeline client-side.
            var all = await query.ToListAsync();
            items = all.OrderByDescending(o => o.Amount).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }
        var custIds = items.Select(i => i.CustomerId).Distinct().ToList();
        ViewBag.CustomerNames = await _db.Customers.Where(c => custIds.Contains(c.CustomerId)).ToDictionaryAsync(c => c.CustomerId, c => c.CustomerName);
        ViewBag.Query = q; ViewBag.Customer = customer; ViewBag.Stage = stage; ViewBag.Status = status;
        ViewBag.Sort = sort; ViewBag.Page = page; ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    // GET /Opportunities/Pipeline — stage columns with totals.
    public async Task<IActionResult> Pipeline()
    {
        var items = await Scoped().Include(o => o.Customer).OrderByDescending(o => o.CreatedDate).ToListAsync();
        ViewBag.TotalPipeline = items.Where(o => o.Stage != "Won" && o.Stage != "Lost").Sum(o => o.Amount);
        ViewBag.TotalWeighted = items.Where(o => o.Stage != "Won" && o.Stage != "Lost").Sum(o => o.Amount * o.Probability / 100);
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var o = await Scoped().Include(x => x.Customer).FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o is null) return await _db.Opportunities.AnyAsync(x => x.OpportunityId == id) ? Forbid() : NotFound();
        return View(o);
    }

    public async Task<IActionResult> Create()
    {
        await LoadListsAsync(null, IsSales() ? Me() : null);
        return View(new Opportunity { Stage = "Qualification", Status = "Open", OwnerId = IsSales() ? Me() : null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity model)
    {
        if (IsSales()) model.OwnerId = Me();
        else if (!await ValidOwnerAsync(model.OwnerId))
            ModelState.AddModelError(nameof(model.OwnerId), "Assigned user must be a Sales Executive.");
        if (await VisibleCustomerAsync(model.CustomerId) is null)
            ModelState.AddModelError(nameof(model.CustomerId), "Customer not found or access denied.");
        model.CreatedDate = DateTime.UtcNow;

        if (!ModelState.IsValid) { await LoadListsAsync(model.CustomerId, model.OwnerId); return WantsJson() ? JsonValidation(400) : View(model); }

        _db.Opportunities.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "Opportunity", model.OpportunityId.ToString());
        TempData["Success"] = "Opportunity saved.";
        return WantsJson() ? CreatedAtAction(nameof(Details), new { id = model.OpportunityId }, ToResult(model)) : RedirectToAction(nameof(Details), new { id = model.OpportunityId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var o = await _db.Opportunities.FindAsync(id);
        if (o is null) return NotFound();
        if (!RecordScope.CanAccess(o.OwnerId, User, Me())) return Forbid();
        await LoadListsAsync(o.CustomerId, o.OwnerId);
        ViewBag.Allowed = OpportunityTransitions.AllowedFrom(o.Stage).ToList();
        return View(o);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity model)
    {
        if (id != model.OpportunityId) return BadRequest();
        var existing = await _db.Opportunities.FindAsync(id);
        if (existing is null) return NotFound();
        if (!RecordScope.CanAccess(existing.OwnerId, User, Me())) return Forbid();
        if (IsSales()) model.OwnerId = existing.OwnerId;
        else if (!await ValidOwnerAsync(model.OwnerId))
            ModelState.AddModelError(nameof(model.OwnerId), "Assigned user must be a Sales Executive.");
        if (await VisibleCustomerAsync(model.CustomerId) is null)
            ModelState.AddModelError(nameof(model.CustomerId), "Customer not found or access denied.");

        // Sync outcome: Won/Lost stage implies matching status and vice versa.
        var targetStage = model.Stage ?? existing.Stage;
        var targetStatus = model.Status ?? existing.Status;
        if (targetStage == "Won" || targetStage == "Lost") targetStatus = targetStage;
        else if (targetStatus == "Won" || targetStatus == "Lost") targetStage = targetStatus;

        var merr = OpportunityTransitions.MoveError(existing.Stage, targetStage);
        if (merr != null) ModelState.AddModelError(nameof(model.Stage), merr);

        if (!ModelState.IsValid) { await LoadListsAsync(model.CustomerId, model.OwnerId); ViewBag.Allowed = OpportunityTransitions.AllowedFrom(existing.Stage).ToList(); return WantsJson() ? JsonValidation(400) : View(model); }

        var oldStage = existing.Stage;
        existing.OpportunityName = model.OpportunityName;
        existing.CustomerId = model.CustomerId;
        existing.LeadId = model.LeadId;
        existing.Stage = targetStage;
        existing.Amount = model.Amount;
        existing.Probability = model.Probability;
        existing.ExpectedCloseDate = model.ExpectedCloseDate;
        existing.Status = targetStatus;
        existing.OwnerId = model.OwnerId;
        existing.Source = model.Source;
        existing.Notes = model.Notes;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(Me(), "Update", "Opportunity", id.ToString(), "Success", $"Stage {oldStage} -> {existing.Stage}");
        TempData["Success"] = existing.Stage is "Won" or "Lost" ? $"Opportunity marked {existing.Stage}." : "Opportunity saved.";
        return WantsJson() ? Ok(ToResult(existing)) : RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var o = await Scoped().Include(x => x.Customer).FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o is null) return await _db.Opportunities.AnyAsync(x => x.OpportunityId == id) ? Forbid() : NotFound();
        return View(o);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var o = await _db.Opportunities.FindAsync(id);
        if (o is null) return NotFound();
        if (!RecordScope.CanAccess(o.OwnerId, User, Me())) return Forbid();
        _db.Opportunities.Remove(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Opportunity", id.ToString());
        TempData["Success"] = "Opportunity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool WantsJson() =>
        Request.Headers.Accept.ToString().Contains("application/json") ||
        (Request.ContentType?.Contains("application/json") ?? false);

    private IActionResult JsonValidation(int status) =>
        StatusCode(status, new { error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray()) } });

    private static object ToResult(Opportunity o) => new
    {
        o.OpportunityId, o.OpportunityName, o.CustomerId, o.LeadId, o.Stage,
        o.Amount, o.Probability, WeightedPipeline = o.Amount * o.Probability / 100,
        o.ExpectedCloseDate, o.Status, o.OwnerId, o.Source, o.Notes, o.CreatedDate
    };
}
