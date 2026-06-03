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
- Candidate portal published job listing/detail/apply APIs, profile GET/PUT API, candidate-owned application history/status API, and candidate-facing interview timeline page.
- Candidate profile vector indexing with `CandidateProfile` source type after profile saves; embedding/vector failures do not block candidate profile updates.
- Recruiter manual sourcing into published Job Posts with invited applications, source metadata, education/work-history capture, and queued invitation email.
- CV Parser Agent for recruiter manual sourcing DOCX uploads. It extracts candidate fields for recruiter review, keeps the parser summary/extracted text hidden from the modal, stores parsed CV evidence in `JobApplicationDocuments`, and upserts `JobApplicationEvidenceProfile` vectors for downstream candidate context/ranking.
- Candidate interview scheduling and interviewer feedback APIs, including candidate/interviewer/hiring-manager scheduling emails, optional calendar metadata, persisted meeting participants, participant realtime scheduling notifications, candidate-profile meeting history, and recruiter feedback notification.
- Hiring Manager final review, editable offer-letter draft storage, in-person offer presentation meeting, final outcome recording, external-candidate fulfillment, and explicit Job Request close.
- Notification read/read-all endpoints.
- Notification outbox processor abstraction, Dapper-backed email processor, worker heartbeat status, and Admin Center outbox diagnostics.
- Tests for the current auth/permission and setup-sensitive logic.

## Planned Next

- Harden auth for real credential login while keeping card login enabled only for local/demo.
- Add more unit/integration tests around Admin Center save flows.
- Extend application evidence capture to cover recruiter/candidate cover letters using the same context and vector path as parsed CV evidence.
- Expand worker to process AI jobs from SQL outbox.
- Add deterministic duplicate checks for candidates if time allows.

## Known Boundaries

- Do not add finance approval or HOD approval workflows for MVP; HOD/department head can participate only as an interviewer user/group.
- Do not automate offer signoff.
- Do not add onboarding, payroll, orientation, or equipment management.
- Do not automate LinkedIn/Indeed posting or scraping.
- Do not let AI make final hiring decisions.
- Local/demo interview scheduling does not create Google Calendar events or Google Meet links because `GoogleCalendar:Enabled` is false. The system stores and emails a pasted meeting link; real calendar events require enabling Google Calendar with service-account credentials and an impersonated organizer.
