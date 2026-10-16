# COMPLIANCE_LEGAL — anti-lawsuit pack (India-first)

> Not legal advice. Have a lawyer review before launch. This file turns your “don’t get sued” list into build tasks + page outlines.

## 1. Pages you must ship (blocked launch if missing)
- `/privacy` – Privacy Policy
- `/terms` – Terms and Conditions
- `/cookies` – Cookie Policy
- `/refunds` – Refund/Cancellation Policy (even for CRM SaaS/trial – state “no refunds on paid plans except…” or “free internal tool – no payments collected” if true; do not omit)
- Footer links to all four + Contact + Business details on every page. Fix footer links (no `#`, no dead links).

### Privacy Policy outline (DPDPA 2023-aligned)
1. Who we are: legal business name, address, CIN/Udyam if applicable, Grievance Officer name/email/phone, response time.
2. Data we collect (minimum only): account (name/email/phone), CRM records user enters, logs (IP, timestamps), support messages. Explicitly list what you DON’T collect.
3. Purpose + lawful basis/consent per purpose; purpose limitation – no reuse beyond stated purpose.
4. Consent: free, specific, informed, unconditional; per-form checkbox (unchecked by default) + link to policy; withdraw/delete request method.
5. Retention: e.g. account data till deletion + 90 days; audit logs 1–3 years for security; backups rolling 30 days.
6. Rights: access, correction, erasure, grievance, nominate; how to contact + timelines (DPDPA: grievance redressal).
7. Security: hashing, access control, HTTPS, backups; breach notification process.
8. Children: no knowing collection under 18 without guardian consent.
9. Cross-border: where hosted (India region preferred); disclose if outside India.
10. Updates + version date + history.

### Terms outline
Identity of business, scope of service, accounts/roles, acceptable use, data ownership (customer owns CRM data), fees/billing if any, termination/suspension, IP, warranties disclaimer, liability cap (to extent allowed), governing law (India) + jurisdiction city, contact.

### Cookie Policy + Consent (do you need it? YES if any non-essential)
- Strictly-necessary (auth session, anti-forgery, load-balance): no consent needed but disclose.
- Anything else (analytics, YouTube, Chat widget, ads, Chart.js CDN is not a cookie but third-party – disclose) → need prior opt-in banner + granular Accept/Reject + re-open preferences + block scripts until consent.
- Implement: banner on first visit, `cookie-preferences` link in footer, consent log (timestamp + choice), no dark patterns.
- If truly zero non-essential cookies + zero analytics: you may use a simple notice, but keep the `/cookies` page anyway – auditors expect it.
- Analytics: default OFF until consent; use privacy-friendly config (IP anonymization, no cross-site sharing, data retention 14 months max, no selling). Disclose provider + purpose + opt-out.

### Refund Policy outline
If paid: plans, billing cycle, GST invoice, 7/14-day window if offered, how to request, timelines, non-refundable setup fees if any, chargeback contact. If free/internal: state “No payments collected; no refunds applicable. Contact for billing queries.” Do not copy another site’s policy.

### Form consent
Every lead/contact/demo form: purpose sentence (“We’ll use your email only to respond to your demo request.”) + unchecked checkbox + link to Privacy + success message (“Thanks – we received your request.”) + error messages. No pre-ticked marketing opt-in. Separate opt-in for newsletter.

## 2. Data minimisation
Collect only: Name, Email/Phone (if needed for contact), message. No DOB, gender, Aadhaar, full address unless needed for billing. Mark optional vs required. Validate + reject extra fields server-side.

## 3. Third-party + tracking audit
Maintain table: Provider | Purpose | Data shared | DPA link | Region. Review quarterly. Pin versions. Remove unused pixels/widgets. Document in Privacy § “Processors”. Examples: hosting, Postgres host, email SMTP, analytics (only with consent), CDN (Bootstrap/Chart.js – SRI + no cookies).

## 4. Business details + content honesty
- Add: legal name, registered address, phone, email, hours, GST/CIN if applicable, Grievance Officer.
- Remove: fake reviews, fake metrics/counters, fake hero claims (“#1 CRM”), unsupported themes/plugins, AI slug photos/copy, placeholder/lorem text. Every testimonial must be real + attributable or removed. Every stat must be sourced or removed.
- Images: only owned/licensed/custom photos (compressed WebP <200KB, alt text). Keep license receipts. No copyrighted Google images. Credit where required. Fix `© {Year} {LegalName}. All rights reserved.` dynamically.

## 5. India DPDPA 2023 checklist
- [ ] Notice + consent per purpose, withdrawable
- [ ] Purpose limitation + minimisation
- [ ] Reasonable safeguards (hashing, RBAC, HTTPS, audit)
- [ ] Retention schedule published + enforced (delete on request)
- [ ] Grievance Officer published
- [ ] Breach notification plan (notify Board + affected users per rules)
- [ ] Children’s data guard
- [ ] Processor contracts (hosting/email) with purpose limits

## 6. Other risks flagged
- Spam (TRAI/UCC): transactional vs promotional SMS/email separation, DLT registration if SMS in India, unsubscribe in 1 click.
- GST invoices if charging; DPDP + IT Act 2000 for logs.
- Accessibility lawsuit risk → see ACCESSIBILITY_SEO_PERFORMANCE (WCAG AA).
- No em-dash, no fake urgency (“Only 2 seats left” unless true).
