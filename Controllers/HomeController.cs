using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Forbidden()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("Home/Error/{code:int?}")]
    public IActionResult Error(int? code)
    {
        if (code == 404)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }
        if (code == 403) return Forbidden();
        // 500 and anything else: generic message only, never a stack trace.
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
