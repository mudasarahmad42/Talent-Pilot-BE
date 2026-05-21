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
- Notification read/read-all endpoints.
- Notification outbox processor abstraction and Dapper-backed processor.
- Tests for the current auth/permission and setup-sensitive logic.

## Planned Next

- Harden auth for real credential login while keeping card login enabled only for local/demo.
- Add more unit/integration tests around Admin Center save flows.
- Implement candidate-facing job listing/detail/apply/profile/application/interview APIs.
- Implement DOCX upload/extraction API boundary.
- Implement recruiter candidate prospect and invite link APIs.
- Implement job post publishing and hiring pipeline command APIs.
- Implement interview scheduling and feedback APIs.
- Implement Hiring Manager final review and offer outcome APIs.
- Add SignalR notification hub and frontend client integration.
- Expand worker to send email and process AI jobs from SQL outbox.
- Add deterministic duplicate checks for candidates if time allows.

## Known Boundaries

- Do not add finance/HOD approval workflow for MVP.
- Do not automate offer signoff.
- Do not add onboarding, payroll, orientation, or equipment management.
- Do not automate LinkedIn/Indeed posting or scraping.
- Do not let AI make final hiring decisions.
