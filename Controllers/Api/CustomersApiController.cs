using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/customers")]
[ApiController]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public CustomersApiController(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private string Me() => RecordScope.CurrentUserId(User);
    private bool IsSales() => User.IsInRole(Roles.SalesExecutive);

    private IQueryable<Customer> Scoped()
    {
        var q = _db.Customers.AsNoTracking().AsQueryable();
        if (IsSales()) q = q.Where(c => c.OwnerId == Me());
        return q;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string sort = "name", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Scoped();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c => c.CustomerName.Contains(q) || c.Email.Contains(q) ||
                c.Phone.Contains(q) || (c.CompanyName != null && c.CompanyName.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        query = sort switch
        {
            "email" => query.OrderBy(c => c.Email),
            "created" => query.OrderByDescending(c => c.CreatedDate),
            _ => query.OrderBy(c => c.CustomerName),
        };
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items = items.Select(ToResult) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await Scoped().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null)
        {
            var exists = await _db.Customers.AnyAsync(x => x.CustomerId == id);
            if (exists) return StatusCode(403, Err("FORBIDDEN", "Access denied."));
            return NotFound(Err("NOT_FOUND", "Customer not found."));
        }
        return Ok(ToResult(c));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        if (await DuplicateAsync(dto.Email, dto.Phone, dto.CustomerCode, null))
            return Conflict(Err("CONFLICT", "Customer with this email/phone already exists."));
        var c = new Customer
        {
            CustomerCode = dto.CustomerCode,
            CustomerName = dto.CustomerName,
            Email = dto.Email, Phone = dto.Phone,
            CompanyName = dto.CompanyName, Address = dto.Address,
            City = dto.City, State = dto.State,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status!,
            OwnerId = IsSales() ? Me() : (dto.OwnerId ?? Me()),
            CreatedBy = User.Identity?.Name, CreatedDate = DateTime.UtcNow
        };
        _db.Customers.Add(c);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(Err("CONFLICT", "Customer with this email/phone already exists.")); }
        await _audit.LogAsync(Me(), "Create", "Customer", c.CustomerId.ToString());
        return CreatedAtAction(nameof(Get), new { id = c.CustomerId }, ToResult(c));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound(Err("NOT_FOUND", "Customer not found."));
        if (!RecordScope.CanAccess(c.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        if (await DuplicateAsync(dto.Email, dto.Phone, dto.CustomerCode, id))
            return Conflict(Err("CONFLICT", "Customer with this email/phone already exists."));
        c.CustomerCode = dto.CustomerCode; c.CustomerName = dto.CustomerName;
        c.Email = dto.Email; c.Phone = dto.Phone;
        c.CompanyName = dto.CompanyName; c.Address = dto.Address;
        c.City = dto.City; c.State = dto.State;
        c.Status = string.IsNullOrWhiteSpace(dto.Status) ? c.Status : dto.Status!;
        if (!IsSales()) c.OwnerId = dto.OwnerId ?? c.OwnerId;
        c.ModifiedDate = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(Err("CONFLICT", "Customer with this email/phone already exists.")); }
        await _audit.LogAsync(Me(), "Update", "Customer", id.ToString());
        return Ok(ToResult(c));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound(Err("NOT_FOUND", "Customer not found."));
        if (!RecordScope.CanAccess(c.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Customer", id.ToString());
        return NoContent();
    }

    private async Task<bool> DuplicateAsync(string email, string phone, string code, int? excludeId)
    {
        var q = _db.Customers.AsNoTracking().Where(c =>
            c.Email.ToLower() == email.ToLower() || c.Phone == phone || c.CustomerCode == code);
        if (excludeId.HasValue) q = q.Where(c => c.CustomerId != excludeId.Value);
        return await q.AnyAsync();
    }

    private object ValidationErr() => new
    {
        error = new
        {
            code = "VALIDATION_ERROR", message = "Validation failed.",
            details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray())
        }
    };

    private static object Err(string code, string message) => new { error = new { code, message } };

    private static object ToResult(Customer c) => new
    {
        c.CustomerId, c.CustomerCode, c.CustomerName, c.Email, c.Phone,
        c.CompanyName, c.Address, c.City, c.State, c.Status, c.OwnerId, c.CreatedDate, c.ModifiedDate
    };
}
