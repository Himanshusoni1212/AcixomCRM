using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AcxiomCRM.Data;

public static class SeedData
{
    public static readonly string[] Roles = ["Admin", "Manager", "SalesExecutive"];
    public const string AdminEmail = "admin@acxiomcrm.local";
    private const string DevAdminPassword = "Admin123!"; // dev fallback only; override via secrets (see below)

    // Dev/test accounts for role verification. Disabled in prod unless Seed:DevUsers=true.
    private static readonly (string Email, string Name, string Role, string Password)[] DevUsers =
    [
        ("manager@acxiomcrm.local", "Test Manager", "Manager", "Manager123!"),
        ("sales@acxiomcrm.local", "Test Sales", "SalesExecutive", "Sales123!"),
    ];

    public static async Task EnsureAsync(IServiceProvider sp)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var env = sp.GetRequiredService<IHostEnvironment>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        // Least privilege: seed secrets come from user-secrets / env / KeyVault, never git.
        // Env names: Seed__AdminPassword, Seed__DevUsers
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = DevAdminPassword;
            log.LogWarning("Seed:AdminPassword not configured; using dev fallback. Set Seed__AdminPassword in production.");
        }

        var seedDevUsers = config.GetValue<bool?>("Seed:DevUsers") ?? env.IsDevelopment();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles)
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail, Email = AdminEmail, EmailConfirmed = true,
                FullName = "CRM Administrator", IsActive = true
            };
            var res = await users.CreateAsync(admin, adminPassword);
            if (res.Succeeded)
                await users.AddToRoleAsync(admin, "Admin");
        }

        if (!seedDevUsers) return;
        foreach (var (email, name, role, password) in DevUsers)
        {
            var u = await users.FindByEmailAsync(email);
            if (u is null)
            {
                u = new ApplicationUser
                {
                    UserName = email, Email = email, EmailConfirmed = true,
                    FullName = name, IsActive = true
                };
                var res = await users.CreateAsync(u, password);
                if (!res.Succeeded) continue;
            }
            // Exactly one primary role: reset then assign.
            var current = await users.GetRolesAsync(u);
            if (current.Count != 1 || !current.Contains(role))
            {
                await users.RemoveFromRolesAsync(u, current);
                await users.AddToRoleAsync(u, role);
            }
        }
    }
}
