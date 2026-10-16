# API_SPEC — REST (§10 + §17.14)

Base: `/api`. JSON only. DTOs, never EF entities. Validate every payload. Auth on every protected endpoint. HTTPS in deploy. Rate-limit auth endpoints. Consistent error object, no stack traces / DB internals / passwords / hashes / tokens.

## Error envelope
```json
{ "error": { "code": "VALIDATION_ERROR", "message": "Opportunity Amount must be greater than 0.", "details": { "Amount": ["Must be > 0"] } } }
```
Status: 200 OK, 201 Created, 400 Validation, 401 Unauth, 403 Forbidden, 404 Not found, 409 Conflict (duplicate email/phone).

## Endpoints
| Method | Endpoint | Purpose | Access |
|---|---|---|---|
| POST | /api/auth/login | Authenticate | Public, rate-limited |
| POST | /api/auth/logout | Terminate session | Authenticated |
| POST | /api/auth/register | Register (if enabled; Admin-gated in prod) | Public/Admin per policy – must lock down |
| GET | /api/customers | List/search (q, status, owner, page, sort) | Authorized (scoped) |
| POST | /api/customers | Create | Authorized |
| GET | /api/customers/{id} | Get one | Authorized (scope check) |
| PUT | /api/customers/{id} | Update | Authorized (scope check) |
| DELETE | /api/customers/{id} | Delete/deactivate + audit | Authorized |
| GET | /api/leads | List/search | Authorized |
| POST | /api/leads | Create | Authorized |
| GET | /api/leads/{id} | Get | Authorized |
| PUT | /api/leads/{id} | Update / status transition | Authorized |
| POST | /api/leads/{id}/convert | Convert → Customer/Opportunity + audit | Authorized |
| GET | /api/opportunities | List/search | Authorized |
| POST | /api/opportunities | Create | Authorized |
| GET | /api/opportunities/{id} | Get | Authorized |
| PUT | /api/opportunities/{id} | Update / stage move | Authorized |
| GET/POST/PUT | /api/followups[...] | Same CRUD pattern | Authorized |
| GET | /api/dashboard/kpis?range=today\|week\|month\|custom | Role-scoped KPIs | Authorized |
| GET | /api/reports/{name} | Filtered reports + export | Authorized (see DASHBOARD_REPORTS) |
| GET | /api/audit?user&module&action&from&to | Filtered audit | Admin (Manager limited) |

## DTO rules
- CustomerDto: CustomerName (req, max 100), Email (req, email, unique), Phone (req, pattern, unique), CompanyName, Address, Status, OwnerId
- LeadDto: LeadName req, Email/Phone common rules, Status in set, ExpectedValue in range
- OpportunityDto: Name req, CustomerId FK must exist, Amount>0 if active, Probability 0–100, ExpectedCloseDate >= today if active
- FollowUpDto: RelatedType+RelatedId must exist, FollowUpDate >= today for new/planned, Status in set
- Never return: PasswordHash, security tokens, internal rowversions beyond need.

## Auth headers
- MVC: cookie + anti-forgery header for SPA POSTs. Express fallback: `Authorization: Bearer <short-lived JWT>` + httpOnly refresh cookie, or session cookie.
- Scope enforcement in service layer: SalesExec → `WHERE OwnerId=@me`; Manager → team scope; Admin → all.
