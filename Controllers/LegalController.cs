using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

// Public legal pages so footer links never 404.
[AllowAnonymous]
public class LegalController : Controller
{
    private readonly AuditService _audit;

    public LegalController(AuditService audit) => _audit = audit;

    public IActionResult Privacy() => View();
    public IActionResult Terms() => View();
    public IActionResult Cookies() => View();
    public IActionResult Refunds() => View();

    // Cookie consent log: stores the visitor choice (accepted/rejected) with timestamp.
    // Logged to the append-only audit store. No personal data is collected here.
    [HttpPost]
    public async Task<IActionResult> Consent([FromForm] string choice)
    {
        if (choice != "accepted" && choice != "rejected")
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Choice must be accepted or rejected." } });
        await _audit.LogAsync("", "Cookie Consent", "Privacy", null, "Success", $"Choice={choice}");
        return Ok(new { message = "Consent recorded." });
    }
}
