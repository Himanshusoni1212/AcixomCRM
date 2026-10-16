using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/followups")]
[ApiController]
[Authorize]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public FollowUpsApiController(ApplicationDbContext db, AuditService audit, UserManager<ApplicationUser> users)
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

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? status, [FromQuery] string? assignee, [FromQuery] string? related,
        [FromQuery] string sort = "date", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Scoped();
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
        query = sort == "created" ? query.OrderByDescending(f => f.FollowUpId) : query.OrderBy(f => f.FollowUpDate);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items = items.Select(ToResult) });
    }

    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming()
    {
        var today = DateTime.UtcNow.Date;
        var items = await Scoped().Where(f => f.Status == "Planned" && f.FollowUpDate.Date >= today).OrderBy(f => f.FollowUpDate).Take(100).ToListAsync();
        return Ok(items.Select(ToResult));
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> Overdue()
    {
        var today = DateTime.UtcNow.Date;
        var items = await Scoped().Where(f => f.Status == "Planned" && f.FollowUpDate.Date < today).OrderBy(f => f.FollowUpDate).Take(100).ToListAsync();
        return Ok(items.Select(ToResult));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var f = await Scoped().FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f is null)
            return await _db.FollowUps.AnyAsync(x => x.FollowUpId == id)
                ? StatusCode(403, Err("FORBIDDEN", "Access denied."))
                : NotFound(Err("NOT_FOUND", "Follow-up not found."));
        return Ok(ToResult(f));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowUpDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        string? assignee = dto.AssignedTo;
        if (IsSales()) assignee = Me();
        else if (!await ValidAssigneeAsync(assignee))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });

        var r = await RelatedRecords.ResolveAsync(_db, dto.RelatedType, dto.RelatedId, IsSales(), Me());
        if (!r.Found)
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Related Customer, Lead or Opportunity not found or access denied." } });

        var f = new FollowUp
        {
            RelatedType = dto.RelatedType, RelatedId = dto.RelatedId,
            CustomerId = r.CustomerId, LeadId = r.LeadId,
            FollowUpDate = dto.FollowUpDate, FollowUpType = dto.FollowUpType,
            Subject = dto.Subject, Status = string.IsNullOrWhiteSpace(dto.Status) ? "Planned" : dto.Status,
            AssignedTo = assignee, Remarks = dto.Remarks, Notes = dto.Notes
        };
        _db.FollowUps.Add(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "FollowUp", f.FollowUpId.ToString(), "Success", $"{f.RelatedType} {f.RelatedId}");
        return CreatedAtAction(nameof(Get), new { id = f.FollowUpId }, ToResult(f));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FollowUpDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound(Err("NOT_FOUND", "Follow-up not found."));
        if (IsSales() && f.AssignedTo != Me())
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));

        var terr = FollowUpTransitions.TransitionError(f.Status, string.IsNullOrWhiteSpace(dto.Status) ? f.Status : dto.Status);
        if (terr != null)
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = terr } });

        string? assignee = dto.AssignedTo;
        if (IsSales()) assignee = f.AssignedTo;
        else if (!await ValidAssigneeAsync(assignee))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });

        var r = await RelatedRecords.ResolveAsync(_db, dto.RelatedType, dto.RelatedId, IsSales(), Me());
        if (!r.Found)
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Related Customer, Lead or Opportunity not found or access denied." } });

        var rescheduled = f.FollowUpDate != dto.FollowUpDate;
        var oldStatus = f.Status;
        f.RelatedType = dto.RelatedType; f.RelatedId = dto.RelatedId;
        f.CustomerId = r.CustomerId; f.LeadId = r.LeadId;
        f.FollowUpDate = dto.FollowUpDate; f.FollowUpType = dto.FollowUpType;
        f.Subject = dto.Subject; f.Status = string.IsNullOrWhiteSpace(dto.Status) ? f.Status : dto.Status;
        f.AssignedTo = assignee; f.Remarks = dto.Remarks; f.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        if (rescheduled)
            await _audit.LogAsync(Me(), "Reschedule", "FollowUp", id.ToString(), "Success", $"Date rescheduled");
        await _audit.LogAsync(Me(), "Update", "FollowUp", id.ToString(), "Success", $"Status {oldStatus} -> {f.Status}");
        return Ok(ToResult(f));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound(Err("NOT_FOUND", "Follow-up not found."));
        if (IsSales() && f.AssignedTo != Me())
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        if (f.Status != "Planned")
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Only planned follow-ups can be completed." } });
        f.Status = "Completed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Complete", "FollowUp", id.ToString(), "Success", $"{f.RelatedType} {f.RelatedId}");
        return Ok(ToResult(f));
    }

    [HttpPost("{id:int}/missed")]
    public async Task<IActionResult> Missed(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound(Err("NOT_FOUND", "Follow-up not found."));
        if (IsSales() && f.AssignedTo != Me())
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        if (f.Status != "Planned")
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Only planned follow-ups can be marked missed." } });
        f.Status = "Missed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Update", "FollowUp", id.ToString(), "Success", "Status Planned -> Missed");
        return Ok(ToResult(f));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f is null) return NotFound(Err("NOT_FOUND", "Follow-up not found."));
        if (IsSales() && f.AssignedTo != Me())
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        _db.FollowUps.Remove(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "FollowUp", id.ToString());
        return NoContent();
    }

    private async Task<bool> ValidAssigneeAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var u = await _users.FindByIdAsync(id);
        return u != null && await _users.IsInRoleAsync(u, Roles.SalesExecutive);
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

    private static object ToResult(FollowUp f) => new
    {
        f.FollowUpId, f.RelatedType, f.RelatedId, f.CustomerId, f.LeadId,
        f.FollowUpDate, f.FollowUpType, f.Subject, f.Status, f.AssignedTo, f.Remarks, f.Notes
    };
}
