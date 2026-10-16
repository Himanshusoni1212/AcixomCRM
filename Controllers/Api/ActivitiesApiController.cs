using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/activities")]
[ApiController]
[Authorize]
public class ActivitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public ActivitiesApiController(ApplicationDbContext db, AuditService audit, UserManager<ApplicationUser> users)
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

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? status, [FromQuery] string? assignee,
        [FromQuery] string sort = "date", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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
        return Ok(new { total, page, pageSize, items = items.Select(ToResult) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var a = await Scoped().FirstOrDefaultAsync(x => x.ActivityId == id);
        if (a is null)
            return await _db.Activities.AnyAsync(x => x.ActivityId == id)
                ? StatusCode(403, Err("FORBIDDEN", "Access denied."))
                : NotFound(Err("NOT_FOUND", "Activity not found."));
        return Ok(ToResult(a));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ActivityDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        string? assignee = dto.AssignedTo;
        if (IsSales()) assignee = Me();
        else if (!await ValidAssigneeAsync(assignee))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });
        if (!await VisibleCustomerAsync(dto.CustomerId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Customer not found or access denied." } });
        if (!await VisibleLeadAsync(dto.LeadId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Lead not found or access denied." } });

        var a = new Activity
        {
            ActivityType = dto.ActivityType, Subject = dto.Subject, Description = dto.Description,
            ActivityDate = dto.ActivityDate, CustomerId = dto.CustomerId, LeadId = dto.LeadId,
            AssignedTo = assignee, Status = string.IsNullOrWhiteSpace(dto.Status) ? "Planned" : dto.Status
        };
        _db.Activities.Add(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "Activity", a.ActivityId.ToString());
        return CreatedAtAction(nameof(Get), new { id = a.ActivityId }, ToResult(a));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActivityDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        var a = await _db.Activities.FindAsync(id);
        if (a is null) return NotFound(Err("NOT_FOUND", "Activity not found."));
        if (IsSales() && a.AssignedTo != Me())
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        if (!IsSales() && !await ValidAssigneeAsync(dto.AssignedTo))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });
        if (!await VisibleCustomerAsync(dto.CustomerId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Customer not found or access denied." } });
        if (!await VisibleLeadAsync(dto.LeadId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Lead not found or access denied." } });

        a.ActivityType = dto.ActivityType; a.Subject = dto.Subject; a.Description = dto.Description;
        a.ActivityDate = dto.ActivityDate; a.CustomerId = dto.CustomerId; a.LeadId = dto.LeadId;
        if (!IsSales()) a.AssignedTo = dto.AssignedTo;
        a.Status = string.IsNullOrWhiteSpace(dto.Status) ? a.Status : dto.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Update", "Activity", id.ToString());
        return Ok(ToResult(a));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a is null) return NotFound(Err("NOT_FOUND", "Activity not found."));
        if (IsSales() && a.AssignedTo != Me())
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        _db.Activities.Remove(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Activity", id.ToString());
        return NoContent();
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

    private object ValidationErr() => new
    {
        error = new
        {
            code = "VALIDATION_ERROR", message = "Validation failed.",
            details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray())
        }
    };

    private static object Err(string code, string message) => new { error = new { code, message } };

    private static object ToResult(Activity a) => new
    {
        a.ActivityId, a.ActivityType, a.Subject, a.Description, a.ActivityDate,
        a.CustomerId, a.LeadId, a.AssignedTo, a.Status
    };
}
