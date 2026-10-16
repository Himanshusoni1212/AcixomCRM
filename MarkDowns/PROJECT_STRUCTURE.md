# PROJECT_STRUCTURE

## ASP.NET Core (evaluation-safe)
```
AcxiomCRM/
 Controllers/ (Account, Dashboard, Customers, Leads, Opportunities, FollowUps, Activities, Users, Roles, AuditLogs, Reports, Api/CustomersController...)
 Models/Entities/ (User ext IdentityUser, Role, Customer, Lead, Opportunity, FollowUp, Activity, AuditLog)
 Data/ (AppDbContext, Configurations, Migrations, Seeds)
 Services/ (CustomerService, LeadService, OpportunityService, FollowUpService, DashboardService, AuditService)
 DTOs/ ViewModels/ (CustomerDto, LeadDto, OppDto, FollowUpDto + list filters)
 Views/ (Shared/_Layout, Account/Login/Register, Dashboard/Index, Customers/..., Legal/Privacy/Terms/Cookies/Refunds)
 wwwroot/ (css/site.css, js/site.js + chart init, favicon.ico, images/ compressed, robots.txt, sitemap.xml)
```

## React+Vite + Express fallback (reference only)
```
web/ (src/pages: Login, Dashboard, Customers, Leads, Opportunities, FollowUps, Activities, Users, Audit, Reports, Legal/*; src/components: Layout, ProtectedRoute, Forms; src/lib/api.js)
server/ (src/routes: auth, customers, leads, opportunities, followups, dashboard, audit; src/middleware: auth, requireRole, scopeFilter, validate; src/services/*; prisma/schema.prisma)
```

## Conventions
- One controller/router per module; service enforces validation + scope + audit.
- No SQL in controllers; no passwords in DTOs; every list supports q/filter/sort/page.
- Legal pages are static + versioned (Privacy v1.0 date) with footer links.
