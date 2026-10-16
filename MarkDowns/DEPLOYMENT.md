# DEPLOYMENT + IMPLEMENTATION SEQUENCE (§15)

## Sequence (§15)
1. Solution structure + database (migrations, seeds: roles + Admin user)
2. User/Role + Identity auth (login/register/logout/lockout)
3. Admin/Manager/SalesExecutive authz
4. Common layout/nav + dashboard shell
5. Customer → Lead → Follow-Up → Opportunity
6. Client + server validation
7. Audit Log
8. REST APIs + authz
9. Reports + KPIs + Chart.js
10. Security/validation/workflow/role testing (§17.19)
11. Prod config: HTTPS, secrets, backups, retention, custom domain, favicon, legal pages

## Deploy notes
- **ASP.NET:** `dotnet publish -c Release`; Azure App Service / IIS / Docker; env vars for connection strings, Identity options (lockout, password), HTTPS redirect, data protection keys persisted.
- **Express fallback:** `npm run build` (Vite) → static + Node service; Postgres managed; `helmet`, `trust proxy`, secure cookies; PM2/Docker.
- Custom domain + TLS (e.g. `crm.example.in`) required before launch. Remove `Made with AI` badge. Add `/privacy`, `/terms`, `/cookies`, `/refunds`.
- Backups daily; audit retention per privacy policy; monitor failed logins + 5xx.

## Env template
```
ConnectionStrings__Default=Server=...;Database=AcxiomCRM;Encrypt=True
Identity__Lockout_MaxFailed=5
Identity__Lockout_Minutes=15
ASPNETCORE_URLS=https://+:443
```
