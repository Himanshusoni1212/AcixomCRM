# AcxiomCRM — README

Source: `/home/gaouravpatil/Downloads/AcxiomCRM_Complete_Project_Assignment.docx`
Scope: Role-based CRM covering lead capture → follow-up → opportunity → reports, with auth, audit, validation, REST API.

## What this is
Web-based CRM for a sales org. Users: **Admin, Manager, Sales Executive**.
Mandatory quality bar (not just CRUD): CRM workflows, client + server validation, business rules, secure auth, RBAC, dashboard analytics, audit logging, REST API, layered architecture.

## Modules (from §2, §4, §17.2)
- App shell: responsive layout, header, user menu, notifications, footer, role-based nav, global search, Bootstrap styling, pagination, alerts, confirm dialogs
- Auth: Login, Register, Logout, Access Control (Identity-based, no custom password table)
- Dashboard: Total Customers, Total Leads, Open Opportunities, Won/Lost, Pipeline, Charts (Chart.js)
- Customer: Create/Edit/Details/Delete/Search, duplicate check, history
- Lead: Create/Edit/Details/Delete, Status, Conversion → Customer/Opportunity
- Opportunity: Create/Edit/Details/Delete, Pipeline stages, Amount×Probability/100 weighted
- Follow-Up: Schedule/Complete/Pending, Planned/Completed/Missed/Cancelled, no backdating
- Activity: Call/Meeting/Email/Task
- User & Role: Users/Roles/Permissions, activate/deactivate, reset password, lockout status
- Audit Log: Login/Create/Update/Delete, filtering
- Reports + REST API

## Docs index
- `ARCHITECTURE.md` – layered architecture
- `TECHSTACK.md` – primary (ASP.NET Core per assignment) + fallback (React+Vite/Express/Postgres)
- `PROJECT_STRUCTURE.md` – folders
- `DATABASE.md` – entities + mandatory fields
- `API_SPEC.md` – endpoints
- `AUTH_SECURITY.md`, `VALIDATION.md`, `WORKFLOWS.md`, `ROLES_PERMISSIONS.md`
- `DASHBOARD_REPORTS.md`, `AUDIT_LOGGING.md`, `UI_UX_DESIGN.md`
- `TESTING_ACCEPTANCE.md`, `SECURITY_CHECKLIST.md`, `DEPLOYMENT.md`
- `COMPLIANCE_LEGAL.md` – Privacy/Terms/Cookie/Refund/DPDPA India
- `ACCESSIBILITY_SEO_PERFORMANCE.md`, `CONTENT_GUARDRAILS.md`, `BACKLOG_TASKS.md`

## How to run

Prerequisites: .NET 8 SDK (`dotnet --version` → 8.x). The app lives in `AcxiomCRM/`.

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
```

### Option A: development (hot reload, dev database `AcxiomCRM/app.db`)

```bash
dotnet run --project AcxiomCRM
# Default profile listens on http://localhost:5190
# (https profile: https://localhost:7063 + http://localhost:5190)
```

### Option B: published Release build (what is live now)

```bash
dotnet publish AcxiomCRM -c Release -o /tmp/acxiom-publish
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="https://127.0.0.1:5001;http://127.0.0.1:5000" \
dotnet /tmp/acxiom-publish/AcxiomCRM.dll
```

Open https://127.0.0.1:5001 (HTTP port 5000 redirects to HTTPS; accept the self-signed dev-cert warning in the browser).

### Sign in (seeded accounts)

- Admin: `admin@acxiomcrm.local` / `Admin123!`
- Manager: `manager@acxiomcrm.local` / `Manager123!`
- Sales: `sales@acxiomcrm.local` / `Sales123!`

Override secrets via environment (never commit real values): `Seed__AdminPassword`, `Seed__DevUsers=false`, `Retention__AuditLogDays`, `ConnectionStrings__Default`.

### Stop

```bash
pkill -f "[A]cxiomCRM.dll"   # published build
pkill -f "[d]otnet run"      # dev servers
```

Key URLs after sign-in: `/Dashboard`, `/Customers`, `/Leads`, `/Opportunities/Pipeline`, `/FollowUps/Upcoming`, `/Reports`, `/AuditLogs` (Admin), `/Legal/Privacy`.

## Acceptance in 60 seconds (§17.19)
1. No auth → protected pages blocked
2. Register/login → Dashboard by scope
3. Invalid email/phone → client validation blocks
4. Crafted request → server still rejects
5. Amount<=0, Probability>100, past Close Date, past Follow-Up → rejected
6. SalesExec sees only assigned; Manager sees team; Admin sees users/roles/audit
7. CRUD → audit entry; `GET /api/customers` → authorized JSON; Dashboard KPIs+charts render

## Launch gates (user-mandated)
Do not launch until: custom domain connected, favicon added, `Made with AI` tag removed, `/privacy` + `/terms` live, cookie consent (if non-essential cookies), business details + contact in footer.
