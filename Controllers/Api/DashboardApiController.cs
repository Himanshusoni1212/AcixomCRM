using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

// Server-authorized chart + KPI JSON. Same scoping as the Dashboard view.
[Route("api/dashboard")]
[ApiController]
[Authorize]
public class DashboardApiController : ControllerBase
{
    private readonly DashboardService _dash;

    public DashboardApiController(DashboardService dash) => _dash = dash;

    [HttpGet("kpis")]
    public async Task<IActionResult> Kpis([FromQuery] string range = "all", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => Ok(await _dash.GetAsync(User, range, from, to));

    [HttpGet("lead-status")]
    public async Task<IActionResult> LeadStatus([FromQuery] string range = "all", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var order = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        var rows = await _dash.LeadStatusAsync(User, range, from, to);
        var map = rows.ToDictionary(r => r.Label, r => r.Value);
        return Ok(new
        {
            labels = order.Where(map.ContainsKey).Concat(map.Keys.Except(order)).ToArray(),
            data = order.Where(map.ContainsKey).Concat(map.Keys.Except(order)).Select(k => map[k]).ToArray()
        });
    }

    [HttpGet("opportunity-pipeline")]
    public async Task<IActionResult> OpportunityPipeline([FromQuery] string range = "all", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var order = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var rows = await _dash.OpportunityPipelineAsync(User, range, from, to);
        var map = rows.ToDictionary(r => r.Label, r => r.Value);
        return Ok(new
        {
            labels = order.Where(map.ContainsKey).Concat(map.Keys.Except(order)).ToArray(),
            data = order.Where(map.ContainsKey).Concat(map.Keys.Except(order)).Select(k => map[k]).ToArray()
        });
    }

    [HttpGet("monthly-sales")]
    public async Task<IActionResult> MonthlySales()
    {
        var rows = await _dash.MonthlySalesAsync(User);
        return Ok(new { labels = rows.Select(r => r.Label).ToArray(), data = rows.Select(r => r.Value).ToArray() });
    }
}
