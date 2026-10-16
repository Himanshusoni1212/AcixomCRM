# SECURITY_CHECKLIST (§14) — verified 16 Oct 2026, see TEST_REPORT.md §3

- [x] Password hashing enabled (Identity default; no plain text)
- [x] Password policy enabled (min length + complexity)
- [x] Account lockout enabled (threshold + duration configurable, audited)
- [x] Role-based authorization enabled (server-side on every action/API)
- [x] Server-side validation enabled (all important requests)
- [x] Anti-forgery enabled for state-changing MVC forms
- [x] Sensitive info excluded from logs (no passwords/hashes/tokens)
- [x] HTTPS in production + Secure/HttpOnly/SameSite cookies + HSTS
- [x] API auth + input validation enabled; DTOs only; correct status codes
- [x] Audit logging enabled for security-sensitive actions
- [x] Least-privilege roles applied
- [x] DB credentials in secrets, not in git; EF parameterized only
- [x] Rate limiting on auth endpoints; generic auth error messages (no user-enumeration)
- [x] Error pages hide stack traces; 403/404 without info leak
