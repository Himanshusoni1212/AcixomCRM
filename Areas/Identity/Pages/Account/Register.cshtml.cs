using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcxiomCRM.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AuditService _audit;

    public RegisterModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AuditService audit)
    {
        _users = users;
        _signIn = signIn;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter your full name.")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a valid email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the Privacy Policy to create an account.")]
        public bool AcceptPrivacy { get; set; }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl ?? Url.Content("~/");

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid) return Page();

        if (await _users.FindByEmailAsync(Input.Email) is not null)
        {
            ModelState.AddModelError(string.Empty, "An account with this email already exists.");
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true,
            FullName = Input.FullName,
            IsActive = true
        };
        var created = await _users.CreateAsync(user, Input.Password);
        if (!created.Succeeded)
        {
            foreach (var e in created.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await _users.AddToRoleAsync(user, Roles.SalesExecutive);
        await _audit.LogAsync(user.Id, "Create", "User", user.Id, "Success", "Self-registration");
        await _signIn.SignInAsync(user, isPersistent: false);
        return LocalRedirect(ReturnUrl);
    }
}
