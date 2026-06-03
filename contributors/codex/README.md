# Codex Contributor Log

## 2026-06-03 - Branch: mudasar-ahmad

- Commit summary: pending strict sequential interview scheduling validation.
- Purpose: validate candidate interview scheduling before calendar creation and return explicit prior-round, duplicate-round, and missing-interviewer errors for direct API callers.
- Files touched: `src/TalentPilot.Application/Operations/OperationsDtos.cs`, `src/TalentPilot.Application/Operations/OperationsInterfaces.cs`, `src/TalentPilot.Application/Operations/OperationsService.cs`, `src/TalentPilot.Infrastructure/Persistence/Repositories/DapperOperationsRepository.cs`, contributor log.
- Endpoints changed: `POST /api/talent-pilot/job-applications/{jobApplicationId}/interviews` can now return `candidate_interview.prior_rounds_pending`, `candidate_interview.round_already_scheduled`, or `candidate_interview.interviewer_required` before any calendar meeting is created.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test "tests\TalentPilot.Tests\TalentPilot.Tests.csproj"` passed with 75 tests.
- Known risks: the backend remains strict; no admin override for out-of-sequence scheduling.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-06-03 - Branch: mudasar-ahmad

- Commit summary: pending interview feedback recruiter notification delivery.
- Purpose: after assigned interview feedback is submitted, queue a richer recruiter email with an application review CTA and return an internal realtime dispatch so SignalR notifies the recruiter to review feedback and schedule the next interview.
- Files touched: `src/TalentPilot.Api/appsettings.Development.json`, `src/TalentPilot.Application/Operations/InterviewScheduleEmailComposer.cs`, `src/TalentPilot.Application/Operations/OperationsDtos.cs`, `src/TalentPilot.Application/Operations/OperationsInterfaces.cs`, `src/TalentPilot.Application/Operations/OperationsService.cs`, `src/TalentPilot.Infrastructure/Persistence/Repositories/DapperOperationsRepository.cs`, contributor log.
- Endpoints changed: `POST /api/talent-pilot/interviews/{interviewId}/feedback` now queues recruiter email and publishes realtime notification dispatches after successful submission; notification snapshot DTOs now include optional metadata.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test` passed with 69 tests.
- Known risks: the recruiter CTA uses `Frontend:BaseUrl` with a localhost fallback and links to the sourcing applications tab; the query `applicationId` is carried for context but the screen does not yet auto-focus that row.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-06-03 - Branch: mudasar-ahmad

- Commit summary: pending historical interview denominator fix.
- Purpose: make historical application interview summaries use the configured interview round count for the linked job post, so partially scheduled applications show `0/3 passed` instead of `0/1 passed` when the job has three active rounds.
- Files touched: `src/TalentPilot.Infrastructure/Persistence/Repositories/DapperOperationsRepository.cs`, contributor log.
- Endpoints changed: response semantics updated for `GET /api/talent-pilot/recruitment/applications/{jobApplicationId}/history`.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test` passed with 69 tests.
- Known risks: older applications without a linked job post fall back to counting request-level interview rounds.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-06-03 - Branch: mudasar-ahmad

- Commit summary: pending notification sender metadata endpoint.
- Purpose: expose admin-only, non-secret email sender metadata from the same Resend/Microsoft Graph options used by the email senders so the Integrations screen can show the actual configured sender mailbox without exposing credentials.
- Files touched: `src/TalentPilot.Api/Controllers/Admin/NotificationsController.cs`, `src/TalentPilot.Application/Admin/Notifications/AdminNotificationDtos.cs`, `knowledge-base/api-surface.md`, contributor log.
- Endpoints changed: added `GET /api/admin/notifications/email-senders`.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test` passed with 69 tests.
- Known risks: the API and worker processes should run with matching email provider configuration so the displayed sender matches worker delivery behavior.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-06-02 - Branch: mudasar-ahmad

- Commit summary: pending tracked candidate invitation links.
- Purpose: create per-recipient candidate invitation links with `CandidateInvitationId` plus raw token, validate them through a public portal resolver, and mark invitations used when candidates submit applications.
- Files touched: `src/TalentPilot.Api/Controllers/OperationsController.cs`, `src/TalentPilot.Application/Operations/OperationsDtos.cs`, `src/TalentPilot.Application/Operations/OperationsInterfaces.cs`, `src/TalentPilot.Application/Operations/OperationsService.cs`, `src/TalentPilot.Infrastructure/Persistence/Repositories/DapperOperationsRepository.cs`, knowledge-base docs, contributor log.
- Endpoints changed: added `GET /api/talent-pilot/portal/invitations/{candidateInvitationId}?token={token}`; extended portal application input with optional `candidateInvitationId` and `invitationToken`.
- Schema changed: no; reuses existing `CandidateInvitations` columns.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test` passed with 63 tests.
- Known risks: backend can only build absolute tracked email links when the recruiter/frontend-provided invitation message includes an absolute candidate portal job URL as the base.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-06-02 - Branch: mudasar-ahmad

- Commit summary: pending candidate invitation HTML email payloads.
- Purpose: send candidate invitation emails with a plain text fallback plus HTML body containing a clickable apply CTA and optional portal hero image.
- Files touched: `src/TalentPilot.Infrastructure/Persistence/Repositories/DapperOperationsRepository.cs`, `knowledge-base/notifications.md`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test` passed with 63 tests.
- Known risks: the email hero image uses the candidate portal URL origin and requires that public frontend asset to be reachable by the recipient email client.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-05-22 - Branch: mudasar-ahmad

- Commit summary: pending backend generic Excel export service and audit log export endpoint.
- Purpose: provide a reusable `DataTable`-based document export service and wire audit logs to `.xlsx` export.
- Files touched: document export DTO/interface/implementation, audit log service/controller, DI, CORS, docs, export service test.
- Endpoints changed: added `GET /api/admin/audit-logs/export`.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test` passed with 4 tests; API smoke test downloaded a valid XLSX payload.
- Known risks: export currently caps audit log rows at 5,000 per request.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-05-22 - Branch: mudasar-ahmad

- Commit summary: pending documentation update for backend engineering instructions.
- Purpose: add pragmatic SOLID and simplicity guidance for backend contributors and AI agents.
- Files touched: `AGENTS.md`, `CONTRIBUTING.md`, `README.md`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: not run; documentation-only update.
- Known risks: none.
- AI assistance: Codex implemented and reviewed the documentation.

## 2026-05-22 - Branch: contributor-guardrails-docs

- Commit summary: pending commit for PMO-to-recruitment operations API slice.
- Purpose: support the internal workflow handoff from PMO to the Recruitment Team.
- Files touched: operations controller, operations DTOs/interfaces/service, Dapper and in-memory operations repositories, operations service tests, knowledge-base docs.
- Endpoints changed: added `GET /api/recruitment/queue` and `POST /api/job-requests/{jobRequestId}/forward-to-recruiter`.
- Schema changed: no new schema file changes in this slice; existing workflow/notification seed records are reused.
- Seed/stored procedures changed: no new seed/stored procedure changes in this slice.
- Tests run: `dotnet test` passed with 10 tests.
- Known risks: database runner was not executed against SQL Server in this slice.
- AI assistance: Codex implemented and reviewed the changes.

## 2026-05-22 - Branch: contributor-guardrails-docs

- Commit summary: pending commit for backend integrations status API and repository guardrails.
- Purpose: continue MVP production-readiness work for Talent Pilot backend.
- Files touched: Admin integrations API/application service, DI registration, operations API, repositories, SQL scripts, README, knowledge-base docs.
- Endpoints changed: added `GET /api/admin/integrations/status`; earlier vertical-slice work added operational Job Request/PMO/notification endpoints.
- Schema changed: previous session work touched schema/seed/stored procedure scripts for operational workflow support.
- Seed/stored procedures changed: workflow and candidate procedure script plus seed reference data were updated in the broader session.
- Tests run: `dotnet test` passed with 8 tests.
- Known risks: database runner was not executed in this documentation guardrail pass.
- AI assistance: Codex implemented and reviewed the changes.
- Guardrail update: added branch/PR policy, contributor logs, and backend security guidelines.

## 2026-05-22 - Branch: contributor-guardrails-docs

- Commit summary: pending commit for multi-agent backend/database navigation documentation.
- Purpose: document how backend/API/database/worker agents should coordinate.
- Files touched: `AGENTS.md`, backend README, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: not run; documentation-only update.
- Known risks: database runner still must be executed by agents that change SQL scripts.
- AI assistance: Codex implemented and reviewed the documentation.

## 2026-05-22 - Branch: contributor-guardrails-docs

- Commit summary: pending commit for PR and merge-conflict contribution rules.
- Purpose: document protected-main workflow and conflict handling for backend contributors.
- Files touched: `CONTRIBUTING.md`, `README.md`, `AGENTS.md`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: not run; documentation-only update.
- Known risks: remote GitHub branch protection still needs repository admin credentials or GitHub connector access.
- AI assistance: Codex implemented and reviewed the documentation.

## 2026-05-22 - Branch: contributor-guardrails-docs

- Commit summary: pending commit for local pre-push main-branch guard.
- Purpose: reduce accidental direct pushes to `main` from local clones.
- Files touched: `.githooks/pre-push`, `README.md`, `CONTRIBUTING.md`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: not run; documentation/hook-only update.
- Known risks: GitHub remote branch protection still requires repository admin configuration.
- AI assistance: Codex implemented and reviewed the hook.
