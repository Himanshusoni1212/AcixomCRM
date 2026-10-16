# ROLES_PERMISSIONS (§7 + §§17.10, 17.14 table)

Three roles. Server-enforced.

## Scope
- **Admin:** full admin – users/roles, audit logs, config, all CRM records + reports.
- **Manager:** team/customer/lead/opportunity/follow-up management + management reports. No unrestricted security admin.
- **SalesExecutive:** assigned customers/leads/opportunities/follow-ups/activities + permitted sales dashboards/reports.

## Suggested matrix (§7.1)
| Module | Admin | Manager | SalesExecutive |
|---|---|---|---|
| Dashboard | Full | Team | Own/Assigned |
| Customers | Full | Team/Business scope | Own/Assigned |
| Leads | Full | Team | Own/Assigned |
| Follow-Ups | Full | Team | Own/Assigned |
| Opportunities | Full | Team | Own/Assigned |
| Activities | Full | Team | Own/Assigned |
| Users/Roles | Full | None/Deny | None/Deny |
| Audit Logs | Full | Limited/view team | None |
| Reports | Full | Management | Scoped sales view |
| Config/Security | Full | None | None |

## Enforcement rules
- `[Authorize(Roles="Admin")]` on user/role/audit-admin endpoints; ownership checks (`OwnerId==UserId` or team membership) for Sales/Manager scopes.
- Every API + MVC action has authz; direct URL/request tampering must 403.
- Exactly one primary role per user unless multi-role explicitly implemented.
- No password hashes / sensitive fields in UI/API.
