# TESTING_ACCEPTANCE (§§13, 17.17, 17.19, 17.20)

## Checklist (§13 table 10 + §17.17)
| Area | Must pass |
|---|---|
| Auth | valid login works; invalid rejected; logout works; anon cannot open protected pages |
| Password | never plain text; policy enforced |
| Lockout | repeated fails trigger lockout; unlock works; audited |
| Authz | cannot access unauthorized pages/records/APIs via URL tampering |
| Client validation | required/email/phone/length/date/numeric before submit |
| Server validation | crafted bypass still rejected |
| Business rules | Amount>0, Prob 0–100, CloseDate>=today, FollowUp>=today, unique email/phone, valid status |
| CRUD | all modules create/read/update/delete or deactivate |
| Dashboard | KPI cards + Chart.js render scoped data |
| Search | required pages filter correctly |
| API | Customer/Lead/Opp endpoints authorized JSON |
| Audit | every important op logged |
| Architecture | Controllers/Models/Data/Services/ViewModels/Views organized |
| Security | no hashes in responses; anti-forgery + protected endpoints |

## Final acceptance script (§17.19) – run in order
1. No auth → blocked. 2. Register/login → Dashboard. 3. Invalid email/phone → client blocks. 4. Crafted POST → server 400. 5. Amount<=0 → 400. 6. Prob>100 → 400. 7. Past CloseDate (active) → 400. 8. Past FollowUp → 400. 9. SalesExec → only assigned. 10. Manager → team pipeline. 11. Admin → users/roles/audit. 12. CRUD → audit row. 13. `GET /api/customers` → 200 scoped JSON. 14. Dashboard → KPIs + 3 charts.

## Completion standard (§17.20)
Fail if only CRUD. Must demo workflows + both validations + business rules + Identity auth + RBAC + analytics + audit + API + layered structure.
