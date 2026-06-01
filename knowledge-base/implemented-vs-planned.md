# Implemented vs Planned

## Implemented

- Clean Architecture solution structure.
- Dapper SQL connection factory and repositories.
- SQL script runner project.
- Idempotent schema, seed, views, and stored procedure scripts.
- Auth login options, card login, refresh, logout, and current user context.
- JWT and refresh token plumbing.
- SQL-backed Admin Center data for:
  - tenant profile
  - users
  - roles
  - access policies
  - groups
  - notification events/templates
  - AI settings/agents/guardrails
  - audit logs
- SQL-backed Talent Pilot operations snapshot.
- Job Request creation endpoint.
- Workflow assignment claim endpoint.
- PMO Review manual employee recommendation, Presales recommendation decision, recruiter handoff, and Bench Matching advisory ranking.
- Recruiter Sourcing queue and first-class Job Post draft/update/publish/close endpoints.
- Talent Rediscovery advisory ranking for claimed Recruiter Sourcing work, persisted in `AiRecommendationLogs` with candidate-profile vector refresh.
- Candidate portal published job listing/detail/apply APIs and candidate-owned application history/status API.
- Recruiter manual sourcing into published Job Posts with invited applications, source metadata, education/work-history capture, and queued invitation email.
- Candidate interview scheduling and interviewer feedback APIs, including candidate/interviewer/hiring-manager scheduling emails and recruiter feedback notification.
- Hiring Manager final review, editable offer-letter draft storage, in-person offer presentation meeting, final outcome recording, external-candidate fulfillment, and explicit Job Request close.
- Notification read/read-all endpoints.
- Notification outbox processor abstraction and Dapper-backed processor.
- Tests for the current auth/permission and setup-sensitive logic.

## Planned Next

- Harden auth for real credential login while keeping card login enabled only for local/demo.
- Add more unit/integration tests around Admin Center save flows.
- Implement candidate profile editing and candidate-facing interview schedule pages.
- Implement DOCX upload/extraction API boundary.
- Add SignalR notification hub and frontend client integration.
- Expand worker to send email and process AI jobs from SQL outbox.
- Add deterministic duplicate checks for candidates if time allows.

## Known Boundaries

- Do not add finance approval or HOD approval workflows for MVP; HOD/department head can participate only as an interviewer user/group.
- Do not automate offer signoff.
- Do not add onboarding, payroll, orientation, or equipment management.
- Do not automate LinkedIn/Indeed posting or scraping.
- Do not let AI make final hiring decisions.
