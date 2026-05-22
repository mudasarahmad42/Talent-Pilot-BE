# API Surface

Keep this file aligned when controllers or frontend contracts change.

## Auth

- `GET /api/auth/login-options`
  - Returns backend-provided MVP login cards.
- `POST /api/auth/login`
  - Card login for MVP or credential login when password flow is enabled.
  - Returns tokens and full user context.
- `POST /api/auth/refresh`
  - Rotates refresh token and returns a new access token.
- `POST /api/auth/logout`
  - Revokes refresh token/session.
- `GET /api/auth/me`
  - Returns current user context from token.

## Admin Center

- `GET /api/admin/tenant-profile`
- `PUT /api/admin/tenant-profile`
- `GET /api/admin/tenant-profile/slug-availability?slug={slug}`
- `GET /api/admin/users`
- `GET /api/admin/users/{userId}`
- `POST /api/admin/users`
- `PUT /api/admin/users/{userId}`
- `PATCH /api/admin/users/{userId}/account-status`
- `POST /api/admin/users/{userId}/invites/resend`
- `GET /api/admin/roles`
- `GET /api/admin/roles/{roleId}`
- `POST /api/admin/roles`
- `PUT /api/admin/roles/{roleId}`
- `PATCH /api/admin/roles/{roleId}/status`
- `GET /api/admin/roles/permissions`
- `POST /api/admin/roles/{roleId}/user-assignment-preview`
- `POST /api/admin/roles/{roleId}/bulk-user-assignments`
- `GET /api/admin/groups`
- `GET /api/admin/access-policies/bench-visibility`
- `PUT /api/admin/access-policies/bench-visibility`
- `GET /api/admin/access-policies/permission-resolution`
- `PUT /api/admin/access-policies/permission-resolution`
- `GET /api/admin/notifications/events`
- `GET /api/admin/notifications/events/{eventId}`
- `PATCH /api/admin/notifications/events/{eventId}/status`
- `GET /api/admin/notifications/templates`
- `PUT /api/admin/notifications/templates/{templateId}`
- `GET /api/admin/ai-settings/runtime`
- `GET /api/admin/ai-settings/agents`
- `GET /api/admin/ai-settings/guardrails`
- `GET /api/admin/integrations/status`
  - Read-only MVP integration status for backend-owned Email outbox, SignalR/in-app notifications, Candidate Portal, LinkedIn mock publishing, DOCX resume parsing, and Ollama/Mock AI runtime.
  - Does not expose paid provider setup, external credentials, or job board automation controls.
- `GET /api/admin/audit-logs`
- `GET /api/admin/audit-logs/{auditLogId}`

## Talent Pilot Internal Operations

- `GET /api/job-requests`
  - Lists job requests visible to the current internal user.
- `POST /api/job-requests`
  - Creates a Job Request, creates a PMO workflow assignment, and queues PMO notifications/outbox rows.
- `GET /api/job-requests/{id}`
- `GET /api/job-requests/{jobRequestId}/bench-matches`
  - Returns primary available/benched internal employee matches with skills, availability/bench status, current allocation, and heuristic match score/explanation.
- `GET /api/pmo/queue`
  - Returns queue items with `assignment.assignedToGroupId` as a stable GUID and `assignment.assignedToGroupName` as display text. Frontend should not route by group display labels.
- `POST /api/workflow-assignments/{assignmentId}/claim`
- `POST /api/job-requests/{jobRequestId}/employee-referrals`
  - PMO handoff to Presales/request owner for selected internal employees. Body: `{ "employeeIds": ["..."], "note": "optional" }`.
  - Requires PMO workflow authorization plus bench-match visibility. Creates `JobRequestEmployeeReferrals`, audit activity, and notification outbox rows without creating candidate applications.
- `GET /api/recruitment/queue`
  - Returns Job Requests assigned to the Recruitment Team after PMO forwards them for sourcing.
- `POST /api/job-requests/{jobRequestId}/forward-to-recruiter`
  - Completes the current PMO assignment, moves the request to Recruiter Sourcing, creates the recruitment assignment, and queues Email/SignalR notifications for the recruitment group.
- `GET /api/notifications`
- `POST /api/notifications/{notificationId}/read`

Legacy/internal aggregate routes:
- `GET /api/talent-pilot/snapshot`
  - Dashboard, work lists, job request list, PMO queue, and notifications snapshot.
- `GET /api/talent-pilot/job-requests/{entityId}/activity`
  - Job request timeline/activity.
- `POST /api/talent-pilot/job-requests`
  - Creates a Job Request and starts the relevant operational path.
- `POST /api/talent-pilot/workflow-assignments/{assignmentId}/claim`
  - Atomic PMO/recruiter ownership claim.
- `PATCH /api/talent-pilot/notifications/{notificationId}/read`
- `PATCH /api/talent-pilot/notifications/read-all`

## Planned API Groups

- Candidate Experience:
  - public jobs
  - job detail
  - candidate registration/login if separate from internal auth
  - apply with DOCX CV
  - candidate profile
  - applications
  - interview schedule
- Recruiter:
  - candidate prospects
  - invite links
  - job post publishing
  - hiring pipeline stage movement
- Interviewer:
  - assigned interviews
  - feedback submission
- Hiring Manager:
  - final review
  - offer outcome recording
- SignalR:
  - notification hub for realtime unread counts and new task events
