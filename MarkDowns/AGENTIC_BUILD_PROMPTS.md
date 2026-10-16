# AGENTIC BUILD PROMPTS — Build AcxiomCRM in 10 Prompts

Use in order with an agentic AI (Claude Code / Codex / OpenCode). Paste one prompt at a time, wait for green tests, then next.
Each prompt references the spec files in `/home/gaouravpatil/axiome`. Do not skip. Do not invent features.

Tech decision: Primary = ASP.NET Core MVC + EF Core + Identity + Bootstrap 5 + Chart.js (required by §§17.4, 17.8, 17.12). If you must use React+Vite + Node+Express + Postgres, keep identical rules/fields/messages – see TECHSTACK.md fallback mapping.

Global bans for all prompts (from CONTENT_GUARDRAILS.md): never use purple gradients, fill-shift buttons, emoji icons, cursor animation, over-the-top scroll animation, fake reviews/metrics/hero text/counters, AI slug photos/copy, em-dashes (use commas/colons), Made-with-AI tag, placeholder/lorem text, copyrighted images. Use neutral slate/navy + white + restrained accent, solid buttons with clear labels, real licensed photos only (compressed WebP + alt text).

---

## Prompt 1 — Scaffold, Database, Identity Auth
```
Read README.md, ARCHITECTURE.md, TECHSTACK.md, DATABASE.md, AUTH_SECURITY.md, PROJECT_STRUCTURE.md.

Scaffold the solution per PROJECT_STRUCTURE.md (ASP.NET Core MVC preferred).
Create all entities with exact fields/constraints from DATABASE.md: User/Role, Customer (CustomerCode UNIQUE, Name req max100, Email req format UNIQUE, Phone req format UNIQUE, duplicate-block), Lead, Opportunity (Amount>0 active, Probability 0-100, ExpectedCloseDate>=today active), FollowUp (FollowUpDate>=today new/planned), Activity, AuditLog (append-only, fields: UserId, Action, EntityName, RecordId, OldValue, NewValue, CreatedDate, IpAddress, Result).

Use ASP.NET Core Identity only for auth (IdentityUser/IdentityRole extended). No custom password table. PBKDF2 hashing, password policy min 8 + complexity, FailedLoginCount + LockoutEnd, lockout after 5 fails for 15 min, reset on success, audit lockout/unlock. Secure cookies HttpOnly+Secure+SameSite=Lax, anti-forgery on POSTs, HTTPS redirect, EF parameterized only, secrets in env not git.

Seed roles Admin/Manager/SalesExecutive + one Admin user. EF migrations + working login/register/logout. Protected pages redirect to login if anon.

Accept: dotnet ef database update succeeds, valid login reaches /Dashboard, invalid rejected, passwords never plain text, no hashes in responses/logs.
```

## Prompt 2 — RBAC + App Shell + Layout
```
Read ROLES_PERMISSIONS.md, UI_UX_DESIGN.md, ACCESSIBILITY_SEO_PERFORMANCE.md.

Implement server-side RBAC (hiding nav is not enough): Admin=full incl users/roles/audit/config, Manager=team scope no security admin, SalesExecutive=own/assigned only. Exactly one primary role per user. Every controller/action + API checks role + ownership (OwnerId==me or team). 403 on tamper.

Build common layout: header + clickable logo to /Dashboard, role-filtered nav, user menu, notifications, footer with legal links + business details placeholders, responsive Bootstrap, mobile hamburger with aria-expanded, skip-to-content, visible focus, no horizontal scroll, tables wrap in scroll container, tap targets >=44px.

Accept: login as each role shows only allowed menu; direct URL to /Users as SalesExec returns 403; 360px mobile has no overflow, menu opens/closes via keyboard.
```

## Prompt 3 — Customer Management + Validation Framework
```
Read DATABASE.md, VALIDATION.md, WORKFLOWS.md, API_SPEC.md.

Build Customer CRUD (Create/Edit/Details/Delete or deactivate/Search): fields CustomerCode, CustomerName req, Email req format unique, Phone req pattern unique (10-digit Indian mobile if configured), Company, Address/City/State, Status, OwnerId, CreatedDate/ModifiedDate. Duplicate email/phone blocked with message "Customer with this email/phone already exists."

Establish reusable validation: client (unobtrusive, inline next to field, block submit) + server (ModelState/DTO, FK exists, ownership, friendly messages no DB internals). Messages: "Customer Name is required.", "Enter a valid email address.", "Enter a valid phone number.", overlong rejected pre-save.

List supports search by Name/Email/Phone/Company + filter/sort/pagination + confirm dialog on delete + success "Customer saved." / error messages. Audit Create/Update/Delete. Labels for all inputs, aria-describedby errors, submit label "Save Customer", phone tel: email mailto: links.

Accept: invalid email/phone blocked client-side AND via crafted POST returns 400; duplicate rejected 409; audit row written.
```

## Prompt 4 — Lead Management + Conversion Workflow
```
Read WORKFLOWS.md, VALIDATION.md, DATABASE.md, AUDIT_LOGGING.md.

Build Lead CRUD + Status machine New/Contacted/Qualified/Unqualified/Converted/Lost + assignment to SalesExecutive. Rules: LeadName mandatory, Status mandatory from set, ExpectedValue in numeric range, email/phone common rules. Reject invalid transitions server-side.

Implement Convert: Qualified Lead -> Customer (+ optional Opportunity), carry contact + ExpectedValue, record conversion in Audit with OldValue/NewValue + Ip. Search by Name/Company/Status/Assigned User.


Accept: New->Contacted->Qualified->Converted works + audit; invalid jump rejected; past data rules enforced; list filters work.
```

## Prompt 5 — Opportunity + Pipeline + Business Rules
```
Read VALIDATION.md, WORKFLOWS.md, DATABASE.md, DASHBOARD_REPORTS.md.

Build Opportunity CRUD + pipeline Qualification/Proposal/Negotiation/Won/Lost. Enforce: Name req, CustomerId FK must exist, Amount>0 if active else "Opportunity Amount must be greater than 0.", Probability 0-100 else "Probability must be between 0 and 100.", ExpectedCloseDate>=today if active else "Expected Close Date cannot be in the past." Weighted = Amount*Probability/100 where required.

Pipeline board (stage columns) + stage moves validated + Won/Lost captures outcome + audit. Search by Name/Customer/Stage/Status.

Accept: Amount<=0, Prob 101, past CloseDate all return 400 with exact messages; weighted calc correct; Won/Lost audited.
```

## Prompt 6 — Follow-Up + Activity Management
```
Read WORKFLOWS.md, VALIDATION.md, DATABASE.md.

Build FollowUp CRUD against Customer/Lead/Opportunity (RelatedType+RelatedId validated): FollowUpDate>=today for new/planned else "Follow-up date cannot be earlier than today." (historical only via explicit authorized flow), Subject/Type/Status Planned/Completed/Missed/Cancelled, AssignedUser, Notes. Views: Upcoming + Overdue. Complete/Missed/Reschedule updates related record + audit.

Build Activity CRUD: Call/Meeting/Email/Task + Subject/Description/ActivityDate/Customer/Lead/AssignedTo/Status. Search FollowUps by Date/Status/Assignee/Related; Activities by Type/Date/Status/Assignee.

Accept: past FollowUp date rejected; upcoming/overdue lists correct; reschedule audited; all messages clear.
```

## Prompt 7 — Dashboard KPIs + Reports + Chart.js
```
Read DASHBOARD_REPORTS.md, ROLES_PERMISSIONS.md.

After login redirect to role-scoped Dashboard. Cards: Total Customers, Total Leads, Open Leads, Total/Open/Won/Lost Opportunities, Total Pipeline Value (SUM open eligible). Filters Today/Week/Month/Custom. Data always scoped: Admin all + user/activity/security stats, Manager team pipeline, SalesExec assigned + upcoming follow-ups.

Charts with Chart.js (no animation excess): Lead Status (New/Contacted/Qualified/Lost/Converted), Opportunity Pipeline (Qualification/Proposal/Negotiation/Won/Lost), Monthly Sales totals. Server-authorized JSON only.

Reports (8) with filters/sort/pagination/export CSV: Customer, Lead, FollowUp, Opportunity, Pipeline (stage+owner+weighted), Sales/Conversion, User Activity, Audit. Audit report Admin only.

Accept: each role sees only scoped numbers; 3 charts render; filters change values; exports work.
```

## Prompt 8 — REST API + Audit Log UI
```
Read API_SPEC.md, AUTH_SECURITY.md, AUDIT_LOGGING.md.

Expose /api with DTOs only (never EF entities, never PasswordHash/tokens): POST /api/auth/login (rate-limited) + logout/register (gated), GET/POST /api/customers + GET/PUT/DELETE /api/customers/{id}, same for /api/leads (+ POST /{id}/convert) and /api/opportunities, CRUD for /api/followups, GET /api/dashboard/kpis, GET /api/reports/{name}, GET /api/audit (Admin). Validate every payload, scope every query, return 200/201/400/401/403/404/409 with {error:{code,message,details}} no stack traces.

Build Audit UI: filter by user/module/action/date, paginated, append-only (no edit/delete buttons), no secrets displayed.

Accept: GET /api/customers returns 200 scoped JSON with auth, 401 without, 403 out-of-scope, 400 invalid DTO, 409 duplicate; CRUD creates audit rows visible in UI.
```

## Prompt 9 — Legal Pages + Cookie Consent + Accessibility/SEO Fixes
```
Read COMPLIANCE_LEGAL.md, ACCESSIBILITY_SEO_PERFORMANCE.md, CONTENT_GUARDRAILS.md, UI_UX_DESIGN.md.

Create /privacy /terms /cookies /refunds with version dates + footer links (no dead # links). Privacy covers: business identity + Grievance Officer contact, minimum-only data list, purpose limitation, unchecked per-form consent + withdraw method, retention (account till delete+90d, audit 1-3y, backups 30d), rights access/correction/erasure, security, children guard, hosting region, updates. Terms covers ownership, acceptable use, fees if any, termination, India governing law. Refunds states policy even if "No payments collected". Every form has purpose sentence + unchecked consent + success/error messages.

Add cookie banner: disclose necessary (auth/anti-forgery) vs opt-in non-essential (analytics/widgets). Block non-essential until Accept, granular Accept/Reject, footer "Cookie Preferences", consent log. Analytics default OFF, IP anonymized if enabled. Remove fake reviews/counters/hero claims, unsupported themes, placeholder text. Add business details (name/address/phone/email/hours), favicon set, unique titles + meta descriptions 120-155 chars, OG/canonical/robots/sitemap, custom compressed real photo per page with alt text, contrast >=4.5, keyboard pass, logo->/Dashboard, tel:/mailto: links, fix copyright to "© 2026 {LegalName}. All rights reserved."

Accept: banner appears first visit, Reject blocks analytics, /privacy + /terms live, grep for lorem/fake claims empty, Lighthouse accessibility >=95, no broken links/buttons.
```

## Prompt 10 — Hardening, Acceptance Tests, Deployment + Launch Gate
```
Read TESTING_ACCEPTANCE.md, SECURITY_CHECKLIST.md, DEPLOYMENT.md.

Harden: HTTPS/HSTS everywhere, rate-limit auth (5/15min), generic auth errors, least-privilege DB creds in secrets, EF parameterized audit, hide stack traces with custom 403/404/500.

Run full acceptance script (§17.19, 14 steps): anon blocked, login->Dashboard, client blocks bad email/phone, crafted POST still 400, Amount<=0/Prob>100/past CloseDate/past FollowUp rejected, role scopes differ, CRUD->audit, /api/customers JSON, KPIs+charts. Verify completion standard (§17.20): workflows + both validations + business rules + Identity + RBAC + analytics + audit + API + layered structure (Controllers/Models/Data/Services/ViewModels/Views). Mobile 360px: no h-scroll/overflow, all pages optimized, images WebP <200KB lazy.

Deploy: publish Release, custom domain + TLS, remove Made-with-AI tag, daily backups, retention job, monitor failed logins/5xx. Do not launch until domain + favicon + legal pages + consent + business details all done.

Accept: SECURITY_CHECKLIST all checked, 14-step script passes, launch gates green. Output test report + remaining risks.
```
