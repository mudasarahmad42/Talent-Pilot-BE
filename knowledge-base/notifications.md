# Notifications

Canonical business source of truth: [../../../TALENT_PILOT_SOURCE_OF_TRUTH.md](../../../TALENT_PILOT_SOURCE_OF_TRUTH.md)

This note is the implementation reference for Admin Center notification configuration and runtime notification delivery.

## Ownership

- Admin route: `/admin-center/notifications`
- Admin API: `/api/admin/notifications/*`
- Runtime API: `/api/talent-pilot/notifications/*`
- Required admin permission: `notifications.manage`
- Test email role gate: `TenantAdmin`
- Event identity: backend code-owned constants in `TalentPilot.Domain.Notifications.NotificationEventCodes`.
- Delivery ownership: backend code owns recipient resolution, email/SignalR enqueueing, retry behavior, and audit history.

Admins can manage editable email copy. They do not create notification events, rename event codes, configure arbitrary transports, retry policy, workflow behavior, or per-row delivery rules from the UI.

## Notification Types

- In-app notification
  - Durable per-user record in `NotificationRecipients`.
  - Returned in `/api/talent-pilot/snapshot` and marked read through the runtime notification endpoints.
  - SignalR is the intended realtime delivery channel; the database record remains the source of truth.
- Email notification
  - Uses a `NotificationTemplates` row linked to a `NotificationEvents` row.
  - Must be enqueued through `NotificationOutbox` with `Channel = Email`.
  - Admins may edit subject and body only.
- SignalR delivery work
  - Must use `NotificationOutbox` with `Channel = SignalR`.
  - Intended for realtime unread-count and new-work delivery.
- Audit record
  - Not a user notification.
  - Admin changes to event status or template content write `AuditLogs` entries for governance.

## Data Model

- `NotificationEvents`
  - Tenant-scoped persistence for the code-owned event catalog.
  - Stores stable `EventCode`, display `Name`, `DefaultRecipientType`, and `Status`.
  - `Status` is `Active` or `Inactive`.
  - `EventCode` must stay stable after release because workflow code and audit history depend on it.
  - Event rows exist to link templates, outbox rows, runtime notifications, and audit history; they are not tenant-authored configuration.
- `NotificationTemplates`
  - Tenant-scoped editable email templates linked to events.
  - Stores `Name`, `Recipient`, `Subject`, `Body`, `AllowedVariablesJson`, `Status`, `UpdatedByUserId`, and timestamps.
  - Template variables use `{{variableName}}`.
  - The application service rejects template updates that use variables outside `AllowedVariablesJson`.
- `NotificationRecipients`
  - Runtime in-app notification rows for specific users.
  - Supports read/read-all behavior through `/api/talent-pilot/notifications/*`.
- `NotificationOutbox`
  - Durable background work queue for delivery.
  - Stores event/template/user/email/channel/payload plus `Pending`, `Processing`, `Sent`, or `Failed` status.

## Seeded Event Catalog

These events are seeded per tenant in `scripts/seed/001_seed_initial_data.sql`.

| Event code | Trigger | Default recipient | Template | Allowed variables |
| --- | --- | --- | --- | --- |
| `PRESALES_REQUEST_SUBMITTED` | Presales submits a resource or job request. Configured department routes notify the PMO recipient; missing department routes email Tenant Admins with application-composed routing-alert copy. | `DepartmentIntakeRoute` | PMO intake email | `jobTitle`, `requesterName` |
| `PMO_EMPLOYEE_REFERRED` | PMO refers an internal employee back to the presales owner. | `User:PresalesOwner` | Employee referral email | `employeeName`, `jobTitle` |
| `PMO_FORWARDED_TO_RECRUITING` | PMO forwards a request to recruiting after bench review. | `Group:Recruiting` | Recruiting handoff email | `jobTitle` |
| `RECRUITER_ASSIGNED_INTERVIEWERS` | Recruiter assigns interviewers to a candidate. | `User:Interviewer` | Interview assignment email | `candidateName`, `jobTitle` |
| `INTERVIEW_FEEDBACK_SUBMITTED` | Interviewer submits feedback; the candidate application returns to recruiter review for next-step decision. | `User:Recruiter` | Feedback received email | `candidateName` |
| `CANDIDATE_STAGE_CHANGED` | Candidate application moves to a new hiring stage. | `User:CandidateOrOwner` | Candidate stage email | `candidateName`, `stageName`, `jobTitle` |
| `HIRING_MANAGER_REVIEW_READY` | Candidate is ready for final hiring-manager review. | `User:HiringManager` | Hiring manager review email | `candidateName`, `jobTitle` |
| `OFFER_PRESENTATION_MEETING_SCHEDULED` | Hiring Manager schedules the in-person offer presentation meeting. | `User:Candidate` | Application-composed offer meeting email | `candidateName`, `jobTitle`, `meetingAt`, `locationText` |

## Recipient Target Rules

- `Group:*` targets a tenant routing group. Example: `Group:PMO`, `Group:Recruiting`.
- `User:*` targets a resolved user role in the workflow context. Example: `User:HiringManager`, `User:Recruiter`.
- `User:CandidateOrOwner` is used when the recipient may be an external candidate or the internal owner, depending on the available payload and channel.
- Recipient strings shown in Admin Center are business targets, not database IDs.

## Template Rules

- Templates are transactional workflow emails, not marketing campaigns.
- Admin-editable fields are `Subject` and `Body`.
- Event code, recipient target, allowed variables, and transport behavior are system-owned.
- Variables must be listed in `AllowedVariablesJson` before they can be used in `Subject` or `Body`.
- Use clear workflow context in copy. Do not include secrets, tokens, or raw IDs in template text.
- If a new variable is needed, add it to the template seed/migration and make sure the event payload supplies it.

## Test Email

- Tenant Admins can send a test email from the Admin Notifications screen.
- The UI posts only the recipient email to `POST /api/admin/notifications/test-email`.
- The backend sends a standalone Resend delivery-check message. It does not use or render notification templates.
- Resend configuration is environment-owned:
  - `Resend:ApiKey` from ASP.NET user-secrets, environment variables, or deployment secrets.
  - `Resend:FromEmail` from appsettings or deployment configuration. Production must use a verified Resend sender/domain.
- The API key must never be committed to appsettings, frontend code, seed scripts, or documentation.
- Successful sends write `NotificationTestEmailSent` audit history as a `NotificationTestEmail` action.

## Realtime Notifications

- Authenticated clients connect to `/hubs/notifications` with the same JWT access token used for API calls.
- Connected clients join tenant and tenant-user SignalR groups:
  - `tenant:{tenantId}` for tenant-wide broadcasts.
  - `tenant:{tenantId}:user:{userId}` for user-targeted workflow messages.
- Server-side code should depend on `IRealtimeNotificationPublisher`, not on `IHubContext`.
- The publisher supports tenant, user, and global sends. Tenant-scoped sends are the default for product workflows.
- Tenant and user realtime sends persist one `NotificationRecipients` row per recipient user before publishing to SignalR.
- `NotificationRecipients` stores title, message, category, severity, optional entity context, metadata JSON, read timestamp, and sent timestamp.
- Clients listen for `NotificationReceived` and receive a `RealtimeNotificationMessage` payload.
- Tenant Admins can send a SignalR test notification from the Admin Notifications screen through `POST /api/admin/notifications/test-realtime`.
- The test endpoint broadcasts to connected clients in the current tenant and writes `NotificationRealtimeTestSent` audit history.
- Production workflow code should publish realtime messages immediately after committing the database notification/outbox transaction.

## Admin Screen Behavior

The Admin Center notifications page should be backed by database APIs only.

- Email templates are the primary configurable list.
- Template list supports search and pagination through `GET /api/admin/notifications/templates`.
- Summary counts come from the database:
  - active event count
  - editable template count
  - pending outbox count
  - failed outbox count
- Template edit screens must save through `PUT /api/admin/notifications/templates/{templateId}`.
- Tenant Admin test email sends must use `POST /api/admin/notifications/test-email`.
- Tenant Admin realtime test sends must use `POST /api/admin/notifications/test-realtime`.
- Topbar notification bells show unread counts from persisted `NotificationRecipients`.
- Clicking the bell opens a drawer grouped by sent date. The drawer lists persisted realtime notifications for the current user and supports mark-read and mark-all-read.
- Template changes write audit history.
- `GET /api/admin/notifications/events` and `GET /api/admin/notifications/events/{eventId}` expose the system event catalog for diagnostics and linking. They should not be presented as tenant-editable objects.

## Current Implementation Boundary

- SQL-backed admin template APIs are implemented.
- Event codes are code-owned constants and mirrored into seeded `NotificationEvents` rows.
- Runtime notification read/read-all APIs are implemented for `NotificationRecipients`.
- A Dapper-backed outbox processor claims due `Pending` email rows, calls `INotificationEmailSender`, then marks rows `Sent` or `Failed` with attempt count, processed timestamp, and last error.
- Admin test email delivery and workflow email delivery are implemented through Resend.
- SignalR hub/client delivery is implemented behind `IRealtimeNotificationPublisher`.
- In local Resend testing, unverified recipient domains may fail provider delivery; failed rows stay in `NotificationOutbox` for diagnostics.

## Adding A New Notification

1. Define the event code, trigger, recipient target, and delivery channels.
2. Add the code to `NotificationEventCodes`.
3. Add a tenant-scoped `NotificationEvents` row through seed or migration.
4. Add at least one `NotificationTemplates` row for email delivery.
5. Keep `AllowedVariablesJson` aligned with the payload the workflow code can provide.
6. Update the workflow/application service to create `NotificationRecipients` and/or `NotificationOutbox` rows when the event occurs.
7. Add tests for recipient resolution, template-variable validation, and enqueue behavior.
8. Update this file and `api-surface.md` if the API contract changes.
