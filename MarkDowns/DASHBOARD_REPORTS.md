# DASHBOARD_REPORTS (§§4.3, 4.11, 11, 17.11–17.13)

## Dashboard (post-login, role-scoped)
Cards (§17.15):
- Total Customers = count visible to user
- Total Leads, Open Leads (not closed/lost/converted)
- Total/Open/Won/Lost Opportunities, Total Pipeline Value = SUM(open eligible Amounts)

Filters: Today / This Week / This Month / Custom Range. Charts server-authorized.

Charts (Chart.js §17.12):
- Lead Status: New, Contacted, Qualified, Lost, Converted (doughnut/bar)
- Opportunity Pipeline: Qualification, Proposal, Negotiation, Won, Lost (funnel/bar)
- Monthly Sales: totals for reporting period (line/bar)

Role views:
- Admin: user/activity/security stats + all KPIs
- Manager: team pipeline, lead/opp performance
- SalesExec: assigned customers/leads/opps + upcoming follow-ups

## Reports (§11 table 9)
| Report | Key data | Access |
|---|---|---|
| Customer | list, status, owner, created date | Admin/Manager/authorized |
| Lead | source, status, owner, conversion | Admin/Manager/sales scope |
| Follow-Up | planned/completed/missed/overdue, date, owner | All authorized |
| Opportunity | stage, amount, probability, close date | All authorized |
| Pipeline | stage-wise + owner-wise + weighted | Admin/Manager; scoped sales |
| Sales/Conversion | lead conversion + opp outcomes | Admin/Manager |
| User Activity | actions + counts | Admin/Manager |
| Audit | security + business events | Admin (limited manager) |

All reports: filters, sorting, pagination, export (CSV/Excel) where implemented.

## Search/Filter (§17.13)
- Customers: Name, Email, Phone, Company
- Leads: Name, Company, Status, Assigned User
- Opportunities: Name, Customer, Stage, Status
- Follow-Ups: Date, Status, Assigned User, Related Customer/Lead
- Activities: Type, Date, Status, Assigned User
