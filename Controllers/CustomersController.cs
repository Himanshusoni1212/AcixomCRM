using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public CustomersController(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private IQueryable<Customer> Scoped()
    {
        var q = _db.Customers.AsNoTracking().AsQueryable();
        if (IsSales())
            q = q.Where(c => c.OwnerId == Me());
        return q;
    }

    // GET /Customers?q=&status=&sort=name&page=1
    public async Task<IActionResult> Index(string? q, string? status, string sort = "name", int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Scoped();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c =>
                c.CustomerName.Contains(q) || c.Email.Contains(q) ||
                c.Phone.Contains(q) || (c.CompanyName != null && c.CompanyName.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        query = sort switch
        {
            "email" => query.OrderBy(c => c.Email),
            "created" => query.OrderByDescending(c => c.CreatedDate),
            _ => query.OrderBy(c => c.CustomerName),
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null) return NotFound();
        if (!RecordScope.CanAccess(c.OwnerId, User, Me())) return Forbid();
        return View(c);
    }

    public IActionResult Create()
    {
        var m = new Customer
        {
            CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            Status = "Active"
        };
        return View(m);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer model)
    {
        // Ownership: sales always owns own records.
        if (IsSales()) model.OwnerId = Me();
        model.CreatedBy = User.Identity?.Name;
        model.CreatedDate = DateTime.UtcNow;

        if (!ModelState.IsValid) return WantsJson() ? JsonValidation(400) : View(model);

        if (await DuplicateAsync(model.Email, model.Phone, model.CustomerCode, null))
            return DuplicateResultAsync(model, "Customer with this email/phone already exists.");

        _db.Customers.Add(model);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return DuplicateResultAsync(model, "Customer with this email/phone already exists."); }

        await _audit.LogAsync(Me(), "Create", "Customer", model.CustomerId.ToString(), "Success", $"Code={model.CustomerCode}");
        TempData["Success"] = "Customer saved.";
        return WantsJson()
            ? CreatedAtAction(nameof(Details), new { id = model.CustomerId }, ToResult(model))
            : RedirectToAction(nameof(Details), new { id = model.CustomerId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        if (!RecordScope.CanAccess(c.OwnerId, User, Me())) return Forbid();
        return View(c);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer model)
    {
        if (id != model.CustomerId) return BadRequest();
        var existing = await _db.Customers.FindAsync(id);
        if (existing is null) return NotFound();
        if (!RecordScope.CanAccess(existing.OwnerId, User, Me())) return Forbid();
        if (IsSales()) model.OwnerId = existing.OwnerId; // sales cannot reassign

        if (!ModelState.IsValid) return WantsJson() ? JsonValidation(400) : View(model);

        if (await DuplicateAsync(model.Email, model.Phone, model.CustomerCode, id))
            return DuplicateResultAsync(model, "Customer with this email/phone already exists.", isEdit: true);

        existing.CustomerCode = model.CustomerCode;
        existing.CustomerName = model.CustomerName;
        existing.Email = model.Email;
        existing.Phone = model.Phone;
        existing.CompanyName = model.CompanyName;
        existing.Address = model.Address;
        existing.City = model.City;
        existing.State = model.State;
        existing.Status = model.Status;
        existing.OwnerId = model.OwnerId;
        existing.ModifiedDate = DateTime.UtcNow;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return DuplicateResultAsync(model, "Customer with this email/phone already exists.", isEdit: true); }

        await _audit.LogAsync(Me(), "Update", "Customer", id.ToString());
        TempData["Success"] = "Customer saved.";
        return WantsJson() ? Ok(ToResult(existing)) : RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null) return NotFound();
        if (!RecordScope.CanAccess(c.OwnerId, User, Me())) return Forbid();
        return View(c);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        if (!RecordScope.CanAccess(c.OwnerId, User, Me())) return Forbid();
        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Customer", id.ToString());
        TempData["Success"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> DuplicateAsync(string email, string phone, string code, int? excludeId)
    {
        var q = _db.Customers.AsNoTracking().Where(c =>
            c.Email.ToLower() == email.ToLower() || c.Phone == phone || c.CustomerCode == code);
        if (excludeId.HasValue) q = q.Where(c => c.CustomerId != excludeId.Value);
        return await q.AnyAsync();
    }

    private bool WantsJson() =>
        Request.Headers.Accept.ToString().Contains("application/json") ||
        (Request.ContentType?.Contains("application/json") ?? false);

    private IActionResult JsonValidation(int status) =>
        StatusCode(status, new { error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray()) } });

    private IActionResult DuplicateResultAsync(Customer model, string message, bool isEdit = false)
    {
        ModelState.AddModelError(string.Empty, message);
        if (WantsJson())
            return StatusCode(409, new { error = new { code = "CONFLICT", message } });
        if (isEdit) return View("Edit", model);
        return View(model);
    }

    private static object ToResult(Customer c) => new
    {
        c.CustomerId, c.CustomerCode, c.CustomerName, c.Email, c.Phone,
        c.CompanyName, c.Address, c.City, c.State, c.Status, c.OwnerId, c.CreatedDate, c.ModifiedDate
    };
}
