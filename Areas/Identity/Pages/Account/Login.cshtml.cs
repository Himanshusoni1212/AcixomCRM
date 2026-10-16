using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcxiomCRM.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;

    public LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, AuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter a valid email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl ?? Url.Content("~/");

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid) return Page();

        var user = await _users.FindByEmailAsync(Input.Email);
        if (user is null || user.IsActive == false)
        {
            await _audit.LogAsync("", "Failed Login", "User", null, "Failure", $"Email={Input.Email}");
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        var result = await _signIn.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            await _audit.LogAsync(user.Id, "Failed Login", "User", user.Id, "Failure", "Locked out");
            ModelState.AddModelError(string.Empty, "Account is locked out. Try again later.");
            return Page();
        }
        if (!result.Succeeded)
        {
            await _audit.LogAsync(user.Id, "Failed Login", "User", user.Id, "Failure", "Invalid password");
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        await _audit.LogAsync(user.Id, "Login", "User", user.Id);
        return LocalRedirect(ReturnUrl);
    }
}
