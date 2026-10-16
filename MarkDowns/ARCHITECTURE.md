# ARCHITECTURE

Technology-independent layered architecture (§3). Implement exactly these separations regardless of language.

## Layers
- **Presentation:** screens, forms, dashboards, nav, client validation, user interaction. Bootstrap responsive. No business SQL here.
- **Application:** request handling, service coordination, DTOs, validation orchestration, workflow orchestration, presentation↔domain bridge.
- **Domain/Business:** entities, core rules, calculations (weighted pipeline), status transitions, business validations, authz rules.
- **Data Access:** repositories, queries, mapping, transactions. EF Core parameterized (no string-concat SQL) or Prisma/parameterized pg.
- **Database:** customers, leads, opportunities, follow-ups, users, roles, activities, audits. FKs, constraints, indexes, normalization.
- **Security:** auth, authz, hashing, session/token, access control, input protection, anti-forgery, HTTPS, rate limiting.
- **Cross-cutting:** logging, exception handling, auditing, config, caching, notifications, monitoring.

## Flow
```
Users
 ↓
Presentation
 ↓
Application (DTOs + validation + workflows)
 ↓
Domain / Business (rules + transitions)
 ↓
Data Access (repos + UoW)
 ↓
Database
Security + Logging + Validation + Auditing → across all layers
```

## Key decisions
1. Server enforces everything client does (§5.2). Never trust browser.
2. Authz on server, not just hidden nav (§7).
3. DTOs at API boundary, never EF entities / password hashes (§4.10, §10.1).
4. Audit append-only, protected from edit/delete (§4.9).
5. HTTPS + secure cookies + anti-forgery for MVC POSTs in prod.

## Alternative mapping (if using React+Vite + Express + Postgres)
- Presentation: React+Vite + React Router + Bootstrap 5 (no custom vibrant theme)
- Application: Express routers + Zod/Joi validation + service layer + DTO mappers
- Domain: service/business-rule modules (status machines, pipeline calc)
- Data Access: Prisma / pg with parameterized queries
- DB: Postgres (same schema as DATABASE.md)
- Security: httpOnly secure cookies or Bearer JWT short-lived + refresh rotation, bcrypt/scrypt/argon2, rate-limit login, helmet, csurf-equivalent (double-submit for SPA)
