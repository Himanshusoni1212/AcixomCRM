using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[Route("api/opportunities")]
[ApiController]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public OpportunitiesApiController(ApplicationDbContext db, AuditService audit, UserManager<ApplicationUser> users)
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

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? customer,
        [FromQuery] string? stage, [FromQuery] string? status,
        [FromQuery] string sort = "name", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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
            // SQLite cannot ORDER BY decimal; sort client-side.
            var all = await query.ToListAsync();
            items = all.OrderByDescending(o => o.Amount).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }
        return Ok(new { total, page, pageSize, items = items.Select(ToResult) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var o = await Scoped().FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o is null)
            return await _db.Opportunities.AnyAsync(x => x.OpportunityId == id)
                ? StatusCode(403, Err("FORBIDDEN", "Access denied."))
                : NotFound(Err("NOT_FOUND", "Opportunity not found."));
        return Ok(ToResult(o));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OpportunityDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        string? ownerId = dto.OwnerId;
        if (IsSales()) ownerId = Me();
        else if (!await ValidOwnerAsync(ownerId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });
        if (!await ScopedCustomers().AnyAsync(c => c.CustomerId == dto.CustomerId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Customer not found or access denied." } });

        var o = new Opportunity
        {
            OpportunityName = dto.OpportunityName, CustomerId = dto.CustomerId, LeadId = dto.LeadId,
            Stage = string.IsNullOrWhiteSpace(dto.Stage) ? "Qualification" : dto.Stage,
            Amount = dto.Amount, Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Open" : dto.Status,
            OwnerId = ownerId, Source = dto.Source, Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow
        };
        _db.Opportunities.Add(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Create", "Opportunity", o.OpportunityId.ToString());
        return CreatedAtAction(nameof(Get), new { id = o.OpportunityId }, ToResult(o));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] OpportunityDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());
        var o = await _db.Opportunities.FindAsync(id);
        if (o is null) return NotFound(Err("NOT_FOUND", "Opportunity not found."));
        if (!RecordScope.CanAccess(o.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        if (!IsSales())
        {
            if (!await ValidOwnerAsync(dto.OwnerId))
                return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Assigned user must be a Sales Executive." } });
        }
        if (!await ScopedCustomers().AnyAsync(c => c.CustomerId == dto.CustomerId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Customer not found or access denied." } });

        var targetStage = string.IsNullOrWhiteSpace(dto.Stage) ? o.Stage : dto.Stage;
        var targetStatus = string.IsNullOrWhiteSpace(dto.Status) ? o.Status : dto.Status;
        if (targetStage == "Won" || targetStage == "Lost") targetStatus = targetStage;
        else if (targetStatus == "Won" || targetStatus == "Lost") targetStage = targetStatus;

        var merr = OpportunityTransitions.MoveError(o.Stage, targetStage);
        if (merr != null)
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = merr } });

        var oldStage = o.Stage;
        o.OpportunityName = dto.OpportunityName; o.CustomerId = dto.CustomerId; o.LeadId = dto.LeadId;
        o.Stage = targetStage; o.Amount = dto.Amount; o.Probability = dto.Probability;
        o.ExpectedCloseDate = dto.ExpectedCloseDate; o.Status = targetStatus;
        if (!IsSales()) o.OwnerId = dto.OwnerId;
        o.Source = dto.Source; o.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Update", "Opportunity", id.ToString(), "Success", $"Stage {oldStage} -> {o.Stage}");
        return Ok(ToResult(o));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var o = await _db.Opportunities.FindAsync(id);
        if (o is null) return NotFound(Err("NOT_FOUND", "Opportunity not found."));
        if (!RecordScope.CanAccess(o.OwnerId, User, Me()))
            return StatusCode(403, Err("FORBIDDEN", "Access denied."));
        _db.Opportunities.Remove(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Me(), "Delete", "Opportunity", id.ToString());
        return NoContent();
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

    private static object ToResult(Opportunity o) => new
    {
        o.OpportunityId, o.OpportunityName, o.CustomerId, o.LeadId, o.Stage,
        o.Amount, o.Probability, WeightedPipeline = o.Amount * o.Probability / 100,
        o.ExpectedCloseDate, o.Status, o.OwnerId, o.Source, o.Notes, o.CreatedDate
    };
}
