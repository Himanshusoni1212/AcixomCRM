# TEST_REPORT — AcxiomCRM Final Acceptance (Prompt 10)

Date: 16 October 2026. App: `AcxiomCRM/` (ASP.NET Core 8 MVC + Identity + EF). Build: 0 warnings, 0 errors. Release publish: `/tmp/acxiom-publish` (92M). Verified on local ports 5146/5147/5149 (dev) and Production profile on 5148/5149.

## 1. §17.19 acceptance script (14 steps) — ALL PASS

| # | Step | Result |
|---|---|---|
| 1 | No auth → blocked | MVC 302 to Login, API 401 (no HTML redirect) |
| 2 | Register/login → Dashboard | Self-register 302 → `/`, Dashboard 200; admin login 302 → Dashboard 200 |
| 3 | Invalid email/phone → client blocks | `data-val`, `data-val-email`, `data-val-regex` present (8 fields); unobtrusive scripts render |
| 4 | Crafted POST → server 400 | API + MVC return 400 `{error:{code,message,details}}` envelope |
| 5 | Amount ≤ 0 → 400 | `Opportunity Amount must be greater than 0.` |
| 6 | Probability > 100 → 400 | `Probability must be between 0 and 100.` |
| 7 | Past ExpectedCloseDate (active) → 400 | `Expected Close Date cannot be in the past.` |
| 8 | Past FollowUpDate (planned) → 400 | `Follow-up date cannot be earlier than today.` |
| 9 | SalesExec → only assigned | customers 1 vs admin 4; `/Users` → 403; audit scoped |
| 10 | Manager → team pipeline | `/Reports/Sales` 200, team KPIs |
| 11 | Admin → users/roles/audit | `/Users` 200, `/api/audit` 200 |
| 12 | CRUD → audit row | API-created customer id appears in `/AuditLogs` UI (module filter) |
| 13 | `GET /api/customers` → authorized JSON | 200 scoped `{total,page,items}`; 401 anon |
| 14 | Dashboard → KPIs + 3 charts | 8 cards + admin stats; Chart.js canvases + `/api/dashboard/*` JSON (lead-status, pipeline, monthly-sales) |

Lockout: 5 bad passwords → 6th attempt `Account is locked out.` (Identity 5 fails / 15 min, audited as Failed Login). Generic errors: unknown user and wrong password both return `Invalid email or password.` (no enumeration).

## 2. §17.20 completion standard — MET (not CRUD-only)

Workflows (lead New→Contacted→Qualified→Converted + customer/opportunity creation, opportunity stage machine, follow-up schedule/complete/reschedule), client + server validation, business rules, Identity auth, server RBAC, dashboard analytics, append-only audit, secured REST API, layered structure (`Controllers/Models/Data/Services/ViewModels/Views`).

## 3. SECURITY_CHECKLIST (§14) — ALL CHECKED

Hashing (PBKDF2, verified hash prefix, never plain), password policy (8 + 4 classes, weak rejected), lockout (5/15min, verified trigger + audit), server RBAC (403s verified), server validation (crafted 400s), anti-forgery (MVC `[ValidateAntiForgeryToken]`, Razor Pages tokens added Prompt 9), no secrets in logs (error bodies scanned: 0 leaks), HTTPS/HSTS prod + Secure/HttpOnly/SameSite=Lax cookies, API DTOs only + status codes, audit on security actions, least-privilege roles (single primary role enforced), no creds in git (SQLite dev string only; `Seed__AdminPassword`/`Seed__DevUsers` via env, warning logged when fallback used), rate limit (API `auth` 20/min/IP → 429; account 5/15min lockout), custom 403/404/500 pages verified in Production (no traces; API JSON preserved).

## 4. Hardening changes (this prompt)

- `Home/Error/{code?}` + `NotFound` view; trimmed `Error` view (removed dev-mode instructions); `UseStatusCodePagesWithReExecute` for non-API routes only.
- `SeedData` reads `Seed:AdminPassword` / `Seed:DevUsers` from configuration (prod disables dev users by default).
- `RetentionService`: purges audit rows older than `Retention:AuditLogDays` (default 1095) at startup with logging; DbContext append-only guard bypassed only for this published-policy path.
- Fixed: template scoped CSS overrode theme (blue links/buttons) and absolute footer overlapped the cookie banner on mobile; footer now static/wrapping, banner a distinct block (screenshot verified at 360px).
- Fixed: `robots.txt` relative sitemap URL (Lighthouse SEO 92 → 100).

## 5. Mobile + Lighthouse + images

- 360px screenshots (Home, Login, Privacy, post-fix Home): no h-scroll/overflow, hamburger nav, stacked buttons, readable footer. Tables use `.table-responsive`; charts responsive.
- Lighthouse mobile (`/Home`): performance 89, accessibility 100, best-practices 100, SEO 100. `/Legal/Privacy` + `/Home` accessibility 100 (Prompt 9).
- Images: icons 2.8–8.4KB PNG (<200KB); zero photographic imagery (no copyright/AI-photo risk, licenses in `AcxiomCRM/docs/image-licenses.csv`); Chart.js 201KB local (no CDN cookie).

## 6. Deploy + launch gates

- `dotnet publish -c Release` green (`/tmp/acxiom-publish`, 92M, wwwroot assets included).
- No `Made with AI` tag anywhere (grep clean). Favicon set served (ico/png/manifest). Legal pages + consent banner + consent audit log live.
- Backups: SQLite `app.db` file copy daily recommended (e.g. `sqlite3 app.db .backup`), Postgres `pg_dump` for prod; retention job runs at startup; failed logins + security events queryable in Audit UI (`Failed Login`, `Complete`, `Convert`).
- Gates: domain + TLS = OPEN (canonical/robots sitemap need absolute URL at launch; HSTS + Secure cookies already coded, TLS must terminate at host). Business details/Grievance contact = PLACEHOLDERS (`support@example.in`, Mumbai address) — replace with real values before launch. Lawyer review of legal copy still required (see COMPLIANCE_LEGAL.md).

## 7. Remaining risks

1. No real TLS verification possible locally; must confirm HSTS header + HTTPS redirect on the TLS-terminated host.
2. Dev seed accounts (`manager@`, `sales@`, default admin password) exist in current `app.db`; production database must be fresh with `Seed__AdminPassword` set and `Seed__DevUsers=false`.
3. Data Protection Board / DLT / GST obligations need a lawyer and real business details (see §6 of COMPLIANCE_LEGAL.md).
4. Lockout test left `sales@acxiomcrm.local` locked for 15 minutes on the test database only.
5. API uses cookie auth without per-request anti-forgery tokens (consistent with MVC-first design); SPA cross-site usage should add header-based anti-forgery (Prompt 10 note).
