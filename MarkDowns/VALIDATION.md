# VALIDATION (§5 + §§17.5–17.8)

Client improves UX; server is mandatory (bypassable client).

## Levels
- **Client (§5.1):** Required, Email, Phone, Length, Date, Numeric + business hints. Inline message next to field. Block submit.
- **Server (§5.2):** repeat all critical checks, ModelState/DTO validation, FK existence, ownership/authz, friendly messages without DB internals.
- **Business (§5.3/17.7):** dates/values/status transitions.

## Type table (§2 table)
| Type | Rule | Example |
|---|---|---|
| Required | Not empty | “Customer Name is required.” |
| Email | Format + uniqueness where required | “Enter a valid email address.” |
| Phone | Pattern/length + uniqueness | “Enter a valid phone number.” (e.g. 10-digit Indian mobile if project rule) |
| Length | Min/max | “Name must be at most 100 characters.” |
| Date | Valid + configured rules | “Expected Close Date cannot be in the past.” |
| Numeric/Range | ExpectedValue range, Amount, Probability | “Probability must be between 0 and 100.” |

## Mandatory business rules
- Opportunity Amount: `>0` for active → else “Opportunity Amount must be greater than 0.”
- Probability: `0–100` inclusive → else “Probability must be between 0 and 100.”
- ExpectedCloseDate: `>= today` for active → else “Expected Close Date cannot be in the past.”
- FollowUpDate: `>= today` for new/planned → else “Follow-up date cannot be earlier than today.” (Historical entry only via explicit authorized flow)
- Customer: Name req + max len; Email req + format + unique; Phone req + format + unique; duplicate customer blocked (“Customer with this email/phone already exists.”)
- Lead: Name mandatory; Status mandatory + in {New, Contacted, Qualified, Unqualified, Converted, Lost}; ExpectedValue in configured range; email/phone common rules
- Lead/Opp/FollowUp status: only valid configured transitions; invalid transition rejected server-side.
- Required blank → field-specific “X is required.” Overlong text → reject before save.

## Implementation
- ASP.NET: DataAnnotations + FluentValidation + Unobtrusive + `[ValidateAntiForgeryToken]`; re-check in service layer.
- Express fallback: Zod schemas shared client/server; same messages; return 400 + details map.
- All forms: labels, `aria-describedby` errors, keyboard-focusable, clear submit labels (e.g. “Save Customer”, not “Submit”).
