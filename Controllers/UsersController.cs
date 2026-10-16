using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

// Admin only. Direct URL as SalesExecutive/Manager must 403 via Authorize Roles.
[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public UsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _users.Users.OrderBy(u => u.Email).ToListAsync();
        var vm = new List<(ApplicationUser, IList<string>)>();
        foreach (var u in list)
            vm.Add((u, await _users.GetRolesAsync(u)));
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string id, string role)
    {
        if (!Roles.All.Contains(role))
        {
            TempData["Error"] = "Invalid role.";
            return RedirectToAction(nameof(Index));
        }
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        // Exactly one primary role: remove all, add one.
        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current);
        await _users.AddToRoleAsync(user, role);
        TempData["Success"] = $"Role for {user.Email} set to {role}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool active)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.IsActive = active;
        user.LockoutEnabled = true;
        if (!active)
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else
            await _users.SetLockoutEndDateAsync(user, null);
        await _users.UpdateAsync(user);
        TempData["Success"] = active ? "User activated." : "User deactivated.";
        return RedirectToAction(nameof(Index));
    }
}
