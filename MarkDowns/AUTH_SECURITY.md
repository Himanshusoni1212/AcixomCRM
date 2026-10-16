# AUTH_SECURITY (§6 + §17.9)

## Authentication features (§6.1/6.2)
- Login with username/email + password; Logout terminates session/cookie and redirects safely.
- Hashing: ASP.NET Core Identity PBKDF2 (or argon2/bcrypt in Express fallback). Never plain text. Never log/display hash, reset token, secrets.
- Password policy: min 8 chars (or higher per org), character-class mix, change/reset via secure token flow, optional recent-password reuse block.
- Session/cookie: HttpOnly, Secure (prod HTTPS), SameSite=Lax/Strict, short idle timeout, anti-forgery for state-changing MVC POSTs (`[ValidateAntiForgeryToken]`).
- Rate limiting/throttling on `/login`, `/register`.

## Lockout (§6.3)
- Track FailedLoginCount. After configurable threshold (e.g. 5 in 15 min) lock for configurable duration (e.g. 15–30 min).
- Reset count on success. Admin unlock via controlled process. Audit lockout + unlock events.

## Authorization (§7)
Enforce on server. Hiding nav is not enough. Roles: Admin, Manager, SalesExecutive. See ROLES_PERMISSIONS.md.

## Anti-abuse
- EF Core / parameterized queries only – no string-concat SQL.
- Validate FKs, ownership, ModelState before DB write. Reject manipulated requests with friendly message, no DB internals.
- Least privilege DB credentials, stored in secrets (user-secrets/KeyVault/env), never in git.
- HTTPS everywhere in prod; HSTS; secure redirects.

## Express fallback parity
helmet, cors allowlist, express-rate-limit (login 5/15min), bcrypt 12, account lockout columns, double-submit CSRF for cookie SPA, JWT 15-min access + rotating refresh.
