# BACKLOG_TASKS — build order (functional + legal + UX)

## P0 – auth + safety
- [ ] Identity setup, password policy, lockout, anti-forgery, HTTPS cookies
- [ ] RBAC middleware + scope filters (Admin/Manager/SalesExec) + 403 pages
- [ ] Server validation for all §17.5–17.8 rules + friendly messages
- [ ] Audit log writer + append-only guard + filters
- [ ] `/privacy`, `/terms`, `/cookies`, `/refunds` pages + footer links + version dates
- [ ] Cookie banner (opt-in for non-essential) + preferences link + consent log
- [ ] Forms: unchecked consent checkbox + purpose sentence + success/error messages

## P1 – CRM core
- [ ] Layout/nav (role-filtered) + mobile menu + clickable logo
- [ ] Customer CRUD + duplicate email/phone check + history
- [ ] Lead CRUD + status machine + convert → Customer/Opportunity + audit
- [ ] Opportunity CRUD + Amount/Probability/Date rules + weighted calc + pipeline board
- [ ] Follow-Up CRUD + Upcoming/Overdue + no backdate + reschedule audit
- [ ] Activity CRUD (Call/Meeting/Email/Task)
- [ ] Users/Roles admin (Admin only) + reset/lockout status
- [ ] Dashboard KPIs + 3 Chart.js charts + Today/Week/Month/Custom filter
- [ ] Reports (8) + search/filter/sort/page + export

## P2 – API
- [ ] DTOs + validation + scoped queries + status codes + error envelope (see API_SPEC)
- [ ] Rate-limit auth, HTTPS, no secrets in responses/logs

## P3 – quality / launch gates
- [ ] Titles + meta descriptions per page, favicon set, custom domain + TLS, remove AI tag
- [ ] Alt text, contrast >=4.5, keyboard pass, focus visible, `tel:`/`mailto:` links
- [ ] Compress images to WebP, custom photo per page, fix copyright line
- [ ] Crawl: zero broken links/buttons, zero horizontal scroll at 360px, zero placeholder text
- [ ] Run TESTING_ACCEPTANCE §17.19 script end-to-end; SECURITY_CHECKLIST all green
- [ ] Business details + Grievance Officer + image licenses filed; DPDPA retention job (delete/anonymize on request)
