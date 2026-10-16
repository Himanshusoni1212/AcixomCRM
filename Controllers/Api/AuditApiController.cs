using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

// Admin-only audit feed. Append-only store; this endpoint is read-only.
// Only safe fields are projected (no secrets exist on AuditLog).
[Route("api/audit")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class AuditApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuditApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? user, [FromQuery] string? module,
        [FromQuery] string? action, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(a => a.UserId == user);
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(a => a.EntityName == module);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.CreatedDate <= to.Value.Date.AddDays(1).AddTicks(-1));
        q = q.OrderByDescending(a => a.CreatedDate);

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new
        {
            total, page, pageSize,
            items = items.Select(a => new
            {
                a.AuditLogId, a.UserId, a.Action, module = a.EntityName,
                recordId = a.RecordId, a.OldValue, a.NewValue,
                a.Result, a.Details, a.IpAddress, a.CreatedDate
            })
        });
    }
}
