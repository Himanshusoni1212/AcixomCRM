# TECHSTACK

## Primary (required by assignment §§17.4, 17.8, 17.12, 17.14)
Assignment explicitly mandates ASP.NET Core, so this is the evaluation-safe default.

- **Backend:** ASP.NET Core MVC (.NET 8 LTS), C#
- **Auth:** ASP.NET Core Identity (IdentityUser/IdentityRole extended), cookie auth, password hasher (PBKDF2 default), lockout, claims/roles, `ValidateAntiForgeryToken`
- **ORM:** Entity Framework Core, parameterized LINQ, migrations, SQL Server (or Npgsql for Postgres)
- **DB:** SQL Server (default) / Postgres (allowed – same schema, use Npgsql provider)
- **Frontend (server-rendered):** Razor Views + Bootstrap 5, jQuery Unobtrusive Validation, Chart.js 4 for dashboard charts
- **API:** ASP.NET Core API controllers, DTOs, DataAnnotations/FluentValidation, status codes 200/201/400/401/403/404/409
- **Why:** §17.4 says “Do not create a custom password table. Use ASP.NET Core Identity.” §17.8 requires unobtrusive/Razor validation. §17.12 requires Chart.js.

## Fallback (user-requested: only if no stack mentioned – but stack IS mentioned)
Use only if evaluator explicitly permits non-.NET implementation. Functionally equivalent:

- **Frontend:** React + Vite, React Router, Bootstrap 5 (plain, accessible), Chart.js via react-chartjs-2, no purple gradients / no cursor animation / no over-the-top scroll
- **Backend:** Node + Express 4/5, helmet, cors, express-rate-limit, cookie-parser
- **DB:** Postgres 15+, Prisma or Knex + pg (parameterized only)
- **Auth:** bcrypt (12) / argon2id, httpOnly Secure SameSite=Lax cookies + short JWT access token, server-side RBAC middleware, account lockout table
- **Validation:** Zod (shared schemas client+server to mirror §5)

## Decision
| Criterion | ASP.NET Core (choose this) | React/Express/PG |
|---|---|---|
| Meets §17.4 Identity rule | Yes | No – must re-implement equivalently, risk fail |
| Meets Razor/unobtrusive rule | Yes | Partial – need to prove parity |
| Hiring/hosting simplicity | IIS/Azure easy | Vercel/Render/VPS easy |
| SPA richness | Lower | Higher |

Recommendation: build Primary for submission. Keep `PROJECT_STRUCTURE.md` React mapping as reference only.

## Versions to pin
- .NET 8, EF Core 8, Identity 8, Bootstrap 5.3, Chart.js 4.4, SQL Server 2022 / Postgres 15
- OR Vite 5, React 18, Express 4.19, pg 8.11, Prisma 5, Zod 3
