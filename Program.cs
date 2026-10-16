using Microsoft.AspNetCore.RateLimiting;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // dev-friendly; set true + email sender in prod
        // Password policy: min 8 + complexity
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        // Lockout: 5 fails -> 15 min
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/Identity/Account/Login";
    o.AccessDeniedPath = "/Home/Forbidden";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    // API clients get status codes, not HTML redirects.
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

// Consistent {error:{code,message,details}} envelope: API controllers validate
// ModelState manually instead of the automatic RFC7807 400.
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);

// Throttle authentication-sensitive endpoints (per IP fixed window).
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", x =>
    {
        x.PermitLimit = 20;
        x.Window = TimeSpan.FromMinutes(1);
        x.QueueLimit = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed roles + admin (never hard-code prod secrets; override via env/user-secrets)
using (var scope = app.Services.CreateScope())
{
    await SeedData.EnsureAsync(scope.ServiceProvider);
    await RetentionService.PurgeAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Friendly 403/404/500 pages for browser routes only.
// API routes keep their JSON {error:{...}} bodies (see cookie events + ApiBehaviorOptions).
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/Error/{0}"));

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllers();
app.MapRazorPages();

app.Run();
