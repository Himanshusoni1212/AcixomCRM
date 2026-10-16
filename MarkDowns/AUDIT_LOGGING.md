# AUDIT_LOGGING (§§4.9, 17.16)

## What to capture
Login success/failure, logout, create/update/delete, role changes, password/security events, important status changes (lead convert, opp stage Won/Lost, follow-up complete/reschedule).

## Fields
§4.9: user, timestamp, action, module, record identifier, result, metadata.
§17.16: UserId, Action, EntityName, RecordId, OldValue, NewValue, CreatedDate, IpAddress where available.

Example events (§17 table 19): Login, Failed Login/lockout, Create/Update/Delete (Customer/Lead/Opp), Role Change, Security admin event (no secrets stored).

## Rules
- Append-only; no app-role update/delete. Only Admin can view (Manager limited team view).
- Never store passwords, hashes, reset tokens, secrets.
- Filter by user, module, action, date range. Paginate.
- Write audit in same transaction as business change where needed; never fail silently – log failures to fallback sink.
- Retain per policy (e.g. 1–3 years); disclose retention in Privacy Policy (DPDPA).
