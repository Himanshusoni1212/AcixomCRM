# DATABASE

## Entities (§9 + §17.3)
Representative fields (§9 table 7) + mandatory fields (§17.3 table 11). Merge both – mandatory wins.

- **User:** UserId PK, Name, Email UNIQUE NOT NULL, PasswordHash (Identity-managed only), RoleId FK, IsActive, FailedLoginCount, LockoutEnd, CreatedDate. Never expose hash in UI/API/logs. One primary role (Admin/Manager/SalesExecutive) unless multi-role explicitly built.
- **Role:** RoleId PK, RoleName UNIQUE (Admin, Manager, SalesExecutive)
- **Customer:** CustomerId PK, CustomerCode UNIQUE, CustomerName NOT NULL (max len, e.g. 100), Email REQUIRED UNIQUE + format, Phone REQUIRED UNIQUE + format (e.g. 10-digit Indian mobile if project rule), CompanyName, Address, City, State, Status, OwnerId FK→User (assigned exec), CreatedDate, ModifiedDate, CreatedBy
- **Lead:** LeadId PK, LeadCode UNIQUE, LeadName NOT NULL, Email + Phone (common rules), CompanyName, Source (Web/Referral/Cold/etc), Status NOT NULL (New/Contacted/Qualified/Unqualified/Converted/Lost), Priority, ExpectedValue NUMERIC range-checked, OwnerId/AssignedTo FK→User, CreatedDate
- **FollowUp:** FollowUpId PK, RelatedType (Customer/Lead/Opportunity), RelatedId, CustomerId/LeadId nullable FKs, FollowUpDate DATE (≥ today for new/planned), FollowUpType, Subject, Status (Planned/Completed/Missed/Cancelled), AssignedTo/AssignedUserId FK→User, Remarks/Notes
- **Opportunity:** OpportunityId PK, OpportunityName, CustomerId FK, LeadId nullable FK, Name, Stage (Qualification/Proposal/Negotiation/Won/Lost), Amount NUMERIC >0 for active, Probability 0–100, ExpectedCloseDate (≥ today if active), OwnerId/AssignedTo FK→User, Source, Notes, Status, CreatedDate
- **Activity:** ActivityId PK, ActivityType (Call/Meeting/Email/Task), Subject, Description, ActivityDate, CustomerId/LeadId nullable FKs, AssignedTo FK→User, Status
- **AuditLog:** AuditLogId PK, UserId, Module/EntityName, Action (Login/FailedLogin/Create/Update/Delete/RoleChange/Security), RecordId, OldValue/NewValue (JSON, no secrets), Timestamp/CreatedDate, Result, Details, IpAddress. Append-only; no update/delete by app roles.

## Relationships
- User 1—N Customer/Lead/Opportunity/FollowUp/Activity (Owner/AssignedTo)
- Customer 1—N Opportunity, FollowUp, Activity
- Lead 1—1 Customer/Opportunity on conversion (keep SourceLeadId)
- All FKs enforced; delete = soft-deactivate where audit matters.

## Constraints / Indexes
- UNIQUE: User.Email, Customer.Email, Customer.Phone, Customer.CustomerCode, Lead.LeadCode, Role.RoleName
- CHECK: Opportunity.Amount>0 (active), Probability BETWEEN 0 AND 100, FollowUpDate>=CURRENT_DATE (new/planned enforced in app + DB check where possible), ExpectedCloseDate>=CURRENT_DATE for active stages
- INDEX: Customer(OwnerId, Status), Lead(Status, AssignedTo), Opportunity(Stage, OwnerId), FollowUp(FollowUpDate, Status, AssignedTo), AuditLog(UserId, EntityName, CreatedDate)
- Normalization 3NF; store hashed passwords only via Identity tables (AspNetUsers etc) – do not duplicate into custom table.

## Postgres DDL sketch (fallback) / SQL Server equivalent
```sql
CREATE TABLE Customers(
 CustomerId SERIAL PRIMARY KEY, CustomerCode VARCHAR(20) UNIQUE NOT NULL,
 CustomerName VARCHAR(100) NOT NULL, Email VARCHAR(255) NOT NULL UNIQUE,
 Phone VARCHAR(20) NOT NULL UNIQUE, CompanyName VARCHAR(150),
 Address TEXT, City VARCHAR(80), State VARCHAR(80),
 Status VARCHAR(30) NOT NULL, OwnerId INT REFERENCES Users(UserId),
 CreatedDate TIMESTAMPTZ DEFAULT now(), ModifiedDate TIMESTAMPTZ
);
-- Leads, Opportunities (Amount NUMERIC(12,2) CHECK(Amount>0), Probability INT CHECK(Probability BETWEEN 0 AND 100)), FollowUps, Activities, AuditLogs (no UPDATE/DELETE grants to app role)
```
