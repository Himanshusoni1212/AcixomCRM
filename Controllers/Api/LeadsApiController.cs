using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/leads")]
[ApiController]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly LeadService _leads;
    private readonly UserManager<ApplicationUser> _users;

    public LeadsApiController(ApplicationDbContext db, AuditService audit, LeadService leads, UserManager<ApplicationUser> users)
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

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? owner,
        [FromQuery] string sort = "name", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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
        return Ok(new { total, page, pageSize, items = items.Select(ToResult) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        if (l is null)
            return await _db.Leads.AnyAsync(x => x.LeadId == id)
                ? StatusCode(403, Err("FORBIDDEN", "Access denied."))
                : NotFound(Err("NOT_FOUND", "Lead not found."));
        return Ok(ToResult(l));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LeadDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        string? ownerId = dto.OwnerId;
        if (IsSales()) ownerId = Me();
        else if (!await ValidOwnerAsync(ownerId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });

        var lead = new Lead
        {
            LeadCode = await _leads.UniqueLeadCodeAsync(),
            LeadName = dto.LeadName, Email = dto.Email, Phone = dto.Phone,
            CompanyName = dto.CompanyName, Source = dto.Source,
            Status = "New", Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue, OwnerId = ownerId,
            Notes = dto.Notes, CreatedDate = DateTime.UtcNow
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "Lead", lead.LeadId.ToString());
        return CreatedAtAction(nameof(Get), new { id = lead.LeadId }, ToResult(lead));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LeadDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        var l = await _db.Leads.FindAsync(id);
        if (l is null) return NotFound(Err("NOT_FOUND", "Lead not found."));
        if (!RecordScope.CanAccess(l.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));

        var terr = LeadService.TransitionError(l.Status, dto.Status);
        if (terr != null)
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = terr } });

        if (!IsSales())
        {
            if (!await ValidOwnerAsync(dto.OwnerId))
                return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });
            l.OwnerId = dto.OwnerId;
        }
        l.LeadName = dto.LeadName; l.Email = dto.Email; l.Phone = dto.Phone;
        l.CompanyName = dto.CompanyName; l.Source = dto.Source;
        l.Status = dto.Status; l.Priority = dto.Priority;
        l.ExpectedValue = dto.ExpectedValue; l.Notes = dto.Notes;
        var oldStatus = l.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Update", "Lead", id.ToString(), "Success", $"Status -> {oldStatus}");
        return Ok(ToResult(l));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l is null) return NotFound(Err("NOT_FOUND", "Lead not found."));
        if (!RecordScope.CanAccess(l.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        _db.Leads.Remove(l);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Lead", id.ToString());
        return NoContent();
    }

    [HttpPost("{id:int}/convert")]
    public async Task<IActionResult> Convert(int id, [FromBody] LeadConvertDto dto)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l is null) return NotFound(Err("NOT_FOUND", "Lead not found."));
        if (!RecordScope.CanAccess(l.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());

        var r = await _leads.ConvertAsync(id, Me(), dto.CreateOpportunity, dto.OpportunityName, dto.Amount, dto.Probability);
        if (!r.Ok)
            return StatusCode(r.ErrorCode == "CONFLICT" ? 409 : 400, Err(r.ErrorCode!, r.Message!));
        return StatusCode(201, new { customerId = r.Customer!.CustomerId, opportunityId = r.Opportunity?.OpportunityId });
    }

    private async Task<bool> ValidOwnerAsync(string? ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return false;
        var u = await _users.FindByIdAsync(ownerId);
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

    private static object ToResult(Lead l) => new
    {
        l.LeadId, l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName,
        l.Source, l.Status, l.Priority, l.ExpectedValue, l.OwnerId, l.Notes, l.CreatedDate
    };
}
