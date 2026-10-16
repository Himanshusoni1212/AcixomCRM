using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public record DashboardData(
    int TotalCustomers,
    int TotalLeads,
    int OpenLeads,
    int TotalOpportunities,
    int OpenOpportunities,
    int WonOpportunities,
    int LostOpportunities,
    decimal TotalPipelineValue,
    int TotalUsers,
    int LockedOutUsers,
    int AuditEvents,
    List<FollowUp> UpcomingFollowUps);

public record ChartPoint(string Label, decimal Value);

// Role-scoped analytics. Admin/Manager see team-wide numbers (v1),
// SalesExecutive sees only assigned records (OwnerId/AssignedTo == me).
public class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public DashboardService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public static bool IsSales(ClaimsPrincipal u) => u.IsInRole(Roles.SalesExecutive);

    // Range: all (default) | today | week (Mon-now) | month (1st-now) | custom (from/to).
    public static (DateTime? From, DateTime? To) ResolveRange(string? range, DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        return range switch
        {
            "today" => (now.Date, null),
            "week" => (now.Date.AddDays(-(int)(now.DayOfWeek == DayOfWeek.Sunday ? 6 : now.DayOfWeek - DayOfWeek.Monday)), null),
            "month" => (new DateTime(now.Year, now.Month, 1), null),
            "custom" => (from?.Date, to?.Date.AddDays(1).AddTicks(-1)),
            _ => (null, null),
        };
    }

    public async Task<DashboardData> GetAsync(ClaimsPrincipal user, string? range, DateTime? from, DateTime? to)
    {
        var me = RecordScope.CurrentUserId(user);
        var sales = IsSales(user);
        var (f, t) = ResolveRange(range, from, to);

        var customers = _db.Customers.AsNoTracking().AsQueryable();
        var leads = _db.Leads.AsNoTracking().AsQueryable();
        var opps = _db.Opportunities.AsNoTracking().AsQueryable();
        if (sales)
        {
            customers = customers.Where(c => c.OwnerId == me);
            leads = leads.Where(l => l.OwnerId == me);
            opps = opps.Where(o => o.OwnerId == me);
        }
        if (f.HasValue)
        {
            customers = customers.Where(c => c.CreatedDate >= f.Value);
            leads = leads.Where(l => l.CreatedDate >= f.Value);
            opps = opps.Where(o => o.CreatedDate >= f.Value);
        }
        if (t.HasValue)
        {
            customers = customers.Where(c => c.CreatedDate <= t.Value);
            leads = leads.Where(l => l.CreatedDate <= t.Value);
            opps = opps.Where(o => o.CreatedDate <= t.Value);
        }

        var openOpp = opps.Where(o => o.Stage != "Won" && o.Stage != "Lost");
        var openAmounts = await openOpp.Select(o => o.Amount).ToListAsync();
        var pipelineValue = openAmounts.Sum();
        var fu = _db.FollowUps.AsNoTracking().AsQueryable();
        if (sales) fu = fu.Where(x => x.AssignedTo == me);
        var upcoming = await fu.Where(x => x.Status == "Planned" && x.FollowUpDate.Date >= DateTime.UtcNow.Date)
            .OrderBy(x => x.FollowUpDate).Take(5).ToListAsync();

        var totalUsers = 0; var locked = 0; var audits = 0;
        if (user.IsInRole(Roles.Admin))
        {
            totalUsers = await _users.Users.CountAsync();
            var lockouts = await _users.Users.Select(u => u.LockoutEnd).ToListAsync();
            locked = lockouts.Count(d => d != null && d > DateTimeOffset.UtcNow);
            var aq = _db.AuditLogs.AsQueryable();
            if (f.HasValue) aq = aq.Where(a => a.CreatedDate >= f.Value);
            if (t.HasValue) aq = aq.Where(a => a.CreatedDate <= t.Value);
            audits = await aq.CountAsync();
        }

        return new DashboardData(
            await customers.CountAsync(),
            await leads.CountAsync(),
            await leads.CountAsync(l => l.Status == "New" || l.Status == "Contacted" || l.Status == "Qualified"),
            await opps.CountAsync(),
            await openOpp.CountAsync(),
            await opps.CountAsync(o => o.Stage == "Won"),
            await opps.CountAsync(o => o.Stage == "Lost"),
            pipelineValue,
            totalUsers, locked, audits, upcoming);
    }

    public async Task<List<ChartPoint>> LeadStatusAsync(ClaimsPrincipal user, string? range, DateTime? from, DateTime? to)
    {
        var me = RecordScope.CurrentUserId(user);
        var q = _db.Leads.AsNoTracking().AsQueryable();
        if (IsSales(user)) q = q.Where(l => l.OwnerId == me);
        var (f, t) = ResolveRange(range, from, to);
        if (f.HasValue) q = q.Where(l => l.CreatedDate >= f.Value);
        if (t.HasValue) q = q.Where(l => l.CreatedDate <= t.Value);
        return await q.GroupBy(l => l.Status).Select(g => new ChartPoint(g.Key, g.Count())).ToListAsync();
    }

    public async Task<List<ChartPoint>> OpportunityPipelineAsync(ClaimsPrincipal user, string? range, DateTime? from, DateTime? to)
    {
        var me = RecordScope.CurrentUserId(user);
        var q = _db.Opportunities.AsNoTracking().AsQueryable();
        if (IsSales(user)) q = q.Where(o => o.OwnerId == me);
        var (f, t) = ResolveRange(range, from, to);
        if (f.HasValue) q = q.Where(o => o.CreatedDate >= f.Value);
        if (t.HasValue) q = q.Where(o => o.CreatedDate <= t.Value);
        return await q.GroupBy(o => o.Stage).Select(g => new ChartPoint(g.Key, g.Count())).ToListAsync();
    }

    // Monthly Won totals for the last 6 calendar months (reporting period).
    public async Task<List<ChartPoint>> MonthlySalesAsync(ClaimsPrincipal user)
    {
        var me = RecordScope.CurrentUserId(user);
        var q = _db.Opportunities.AsNoTracking().Where(o => o.Stage == "Won");
        if (IsSales(user)) q = q.Where(o => o.OwnerId == me);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var rows = await q.Where(o => o.CreatedDate >= start).ToListAsync();
        var months = Enumerable.Range(0, 6).Select(i => start.AddMonths(i)).ToList();
        return months.Select(m => new ChartPoint(
            m.ToString("MMM yyyy"),
            rows.Where(o => o.CreatedDate.Year == m.Year && o.CreatedDate.Month == m.Month).Sum(o => o.Amount))).ToList();
    }
}
