using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

// Cookie authentication for API clients. No tokens or hashes are ever returned.
// Login/register are rate-limited per IP (see Program.cs "auth" policy).
[Route("api/auth")]
[ApiController]
public class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;

    public AuthApiController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, AuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _audit = audit;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());

        var user = await _users.FindByEmailAsync(dto.Email);
        if (user is null || user.IsActive == false)
        {
            await _audit.LogAsync("", "Failed Login", "User", null, "Failure", $"Email={dto.Email}");
            return Unauthorized(Err("UNAUTHORIZED", "Invalid email or password."));
        }

        var result = await _signIn.PasswordSignInAsync(user, dto.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            await _audit.LogAsync(user.Id, "Failed Login", "User", user.Id, "Failure", "Locked out");
            return Unauthorized(Err("UNAUTHORIZED", "Account is locked out. Try again later."));
        }
        if (!result.Succeeded)
        {
            await _audit.LogAsync(user.Id, "Failed Login", "User", user.Id, "Failure", "Invalid password");
            return Unauthorized(Err("UNAUTHORIZED", "Invalid email or password."));
        }

        var roles = await _users.GetRolesAsync(user);
        await _audit.LogAsync(user.Id, "Login", "User", user.Id);
        return Ok(new { email = user.Email, roles });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var id = _users.GetUserId(User) ?? "";
        await _signIn.SignOutAsync();
        await _audit.LogAsync(id, "Logout", "User", id);
        return Ok(new { message = "Signed out." });
    }

    // Locked down: only Admins can create users via the API.
    [HttpPost("register")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (dto is null || !ModelState.IsValid) return BadRequest(ValidationErr());

        if (await _users.FindByEmailAsync(dto.Email) is not null)
            return Conflict(Err("CONFLICT", "A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(dto.FullName) ? dto.Email : dto.FullName!,
            IsActive = true
        };
        var created = await _users.CreateAsync(user, dto.Password);
        if (!created.Succeeded)
            return BadRequest(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "Password does not meet the policy.",
                    details = created.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })
                }
            });

        await _users.AddToRoleAsync(user, dto.Role);
        var by = _users.GetUserId(User) ?? "";
        await _audit.LogAsync(by, "Create", "User", user.Id, "Success", $"Role={dto.Role}");
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { id = user.Id, email = user.Email, role = dto.Role });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized(Err("UNAUTHORIZED", "Not signed in."));
        return Ok(new { email = user.Email, roles = await _users.GetRolesAsync(user) });
    }

    [HttpGet("users/{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound(Err("NOT_FOUND", "User not found."));
        return Ok(new { id = user.Id, email = user.Email, fullName = user.FullName, isActive = user.IsActive, roles = await _users.GetRolesAsync(user) });
    }

    private object ValidationErr() => new
    {
        error = new
        {
            code = "VALIDATION_ERROR",
            message = "Validation failed.",
            details = ModelState.ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray())
        }
    };

    private static object Err(string code, string message) => new { error = new { code, message } };
}
