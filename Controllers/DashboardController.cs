using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dash;

    public DashboardController(DashboardService dash) => _dash = dash;

    // GET /Dashboard?range=all|today|week|month|custom&from=&to=
    public async Task<IActionResult> Index(string range = "all", DateTime? from = null, DateTime? to = null)
    {
        var data = await _dash.GetAsync(User, range, from, to);
        ViewBag.Range = range;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(data);
    }
}
