# WORKFLOWS (§8)

## Lead-to-Customer
1. Create Lead (Name req, Source, Status=New, Owner assigned)
2. Assign Lead → SalesExecutive
3. Contact Lead (New→Contacted)
4. Qualify (Contacted→Qualified) or mark Unqualified/Lost
5. Convert Qualified → Customer (+ optional Opportunity), carry contact + ExpectedValue
6. Audit conversion (OldValue/NewValue, UserId, Ip)

Invalid transition (e.g. New→Converted without Qualify, if disallowed) → server rejects.

## Opportunity
1. Create (CustomerId must exist, Name req)
2. Set Amount (>0 if active) + Probability (0–100) + ExpectedCloseDate (>=today if active)
3. Move Qualification→Proposal→Negotiation (validate each move)
4. Mark Won/Lost + capture final outcome + audit
5. Weighted pipeline = Amount × Probability / 100 where required

## Follow-Up
1. Create against Customer/Lead/Opportunity (RelatedType+RelatedId validated)
2. Set Date (>=today for new/planned), Subject, Type, AssignedUser
3. Assign + notify; show in Upcoming/Overdue lists
4. Complete/Missed/Reschedule → update related record + audit

## User Administration
1. Create User → Assign exactly one Role → Set Active/Inactive
2. Apply password/lockout policy → audit security change
3. Reset password only via admin-triggered secure flow; show lockout status; never expose hashes.
