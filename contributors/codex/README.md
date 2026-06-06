# Codex Contributor Log

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending Online Headhunting search tuning.
- Purpose: improve AI Headhunting recall for roles like Senior Python Developer in Lahore by generating strict plus broader source queries, preserving primary role skills in GitHub search, and allowing unknown-location profiles when they came from a location-constrained search query while still rejecting explicit non-target locations.
- Files touched: `src/TalentPilot.Application/Ai/OnlineHeadhuntingAgent.cs`, `src/TalentPilot.Infrastructure/Ai/GitHubCandidateSearchProvider.cs`, `tests/TalentPilot.Tests/Ai/OnlineHeadhuntingAgentTests.cs`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --no-restore --filter OnlineHeadhuntingAgentTests` passed with 7 tests; full `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --no-restore` passed with 130 tests.
- Known risks: existing saved no-result runs remain unchanged until the recruiter runs Online Headhunting again; provider API quotas and credentials still control live search availability.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending department-aware AI requirement matching.
- Purpose: extend the centralized exact/adjacent/transferable/broad/missing matcher beyond engineering so agents evaluate Sales, Presales, HR/Recruitment, Finance, Marketing, Customer Success, QA, Project/Product, DevOps/Cloud, and Data/BI sub-domains without inflating broad department labels.
- Files touched: `src/TalentPilot.Application/Ai/TechnologySkillMatcher.cs`, bench/applicant/rediscovery/drafter/parser/interview AI prompt text, `tests/TalentPilot.Tests/Ai/TechnologySkillMatcherTests.cs`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --no-restore --filter TechnologySkillMatcherTests` passed with 19 tests; full `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --no-restore` passed with 129 tests.
- Known risks: detailed requirement-level assessments remain runtime-derived and folded into existing agent explanation fields; no persisted department-fit assessment schema was added in this pass.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending technology-specific AI skill matching.
- Purpose: centralize exact/adjacent/transferable/broad/missing skill assessment so AI agents stop inflating broad backend/frontend labels and clearly warn when required technologies such as Python are not directly evidenced.
- Files touched: `src/TalentPilot.Application/Ai/TechnologySkillMatcher.cs`, bench/applicant/rediscovery/online/drafter/parser/interview AI agents, `src/TalentPilot.Infrastructure/Persistence/Repositories/DapperOperationsRepository.cs`, `tests/TalentPilot.Tests/Ai/TechnologySkillMatcherTests.cs`, agent tests, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --no-restore` passed with 118 tests.
- Known risks: detailed skill assessment is currently computed at runtime and folded into existing agent response fields; no persisted per-skill assessment schema was added in this pass.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending talent rediscovery LLM JSON fallback.
- Purpose: keep Talent Rediscovery ranking available when the LLM returns wrapped, prose-prefixed, partial, empty, or malformed explanation JSON; the prompt now requires a raw top-level JSON array and the agent falls back to deterministic recruiter-facing explanations when needed.
- Files touched: `src/TalentPilot.Application/Ai/TalentRediscoveryAgent.cs`, `tests/TalentPilot.Tests/Ai/TalentRediscoveryAgentTests.cs`, contributor log.
- Endpoints changed: no.
- Schema changed: no.
- Seed/stored procedures changed: no.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --filter TalentRediscoveryAgentTests` passed with 9 tests.
- Known risks: fallback explanations are deterministic summaries from tenant evidence, so they may be less nuanced than a successful LLM explanation but will not block recruiter sourcing.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending PMO referral answer clarity.
- Purpose: make Request Copilot answer PMO Presales referral questions with a clear stance, especially when the same evidence shows missing required skills; contradictory saved answers now say not to refer yet.
- Files touched: `src/TalentPilot.Application/AiAssistant/RagPromptBuilder.cs`, `src/TalentPilot.Application/AiAssistant/AiAssistantService.cs`, `src/TalentPilot.Application/AiAssistant/RagAnswerSanitizer.cs`, `tests/TalentPilot.Tests/AiAssistant/RagPromptBuilderTests.cs`, `scripts/migrations/040_clarify_pmo_presales_referral_answer.sql`, contributor log.
- Endpoints changed: no.
- Schema changed: no schema shape change; added idempotent data migration to clarify existing saved PMO assistant answers.
- Seed/stored procedures changed: no seed or stored procedure changes.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --filter "RagPromptBuilderTests|RagCitationUsageTests|KnowledgeIndexingServiceTests"` passed with 12 tests; database script runner completed successfully; direct DB check confirmed zero old `Refer Zain Javaid...` answers and one corrected `Do not refer...` answer.
- Known risks: The assistant remains decision support; PMO still owns the actual workflow action.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending RAG answer source-label cleanup.
- Purpose: stop conversational assistant answers from echoing internal evidence metadata such as `(BenchMatch, BenchMatchLog)` or `(BenchEmployee, BenchEmployeeProfile)` in natural-language replies.
- Files touched: `src/TalentPilot.Application/AiAssistant/RagPromptBuilder.cs`, `src/TalentPilot.Application/AiAssistant/AiAssistantService.cs`, `src/TalentPilot.Application/AiAssistant/RagAnswerSanitizer.cs`, `tests/TalentPilot.Tests/AiAssistant/RagPromptBuilderTests.cs`, `scripts/migrations/039_sanitize_rag_technical_source_labels.sql`, contributor log.
- Endpoints changed: no.
- Schema changed: no schema shape change; added idempotent data migration to clean existing saved assistant messages.
- Seed/stored procedures changed: no seed or stored procedure changes.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --filter "RagPromptBuilderTests|RagCitationUsageTests|KnowledgeIndexingServiceTests"` passed with 10 tests; database script runner completed successfully; direct DB check confirmed zero saved assistant messages with the internal tuple labels.
- Known risks: reference chips and evidence previews still retain structured metadata for source inspection; only the conversational answer text is sanitized.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending bench match rationale correction.
- Purpose: prevent stale PMO copilot evidence from claiming that Zain Javaid's 6.8 years of experience is less than a 3+ years requirement; saved bench-match evidence now uses the guarded Java-profile/required-skill-gap rationale.
- Files touched: `src/TalentPilot.Application/Ai/BenchMatchExplanationGuard.cs`, `src/TalentPilot.Application/Ai/BenchMatchingAgent.cs`, `src/TalentPilot.Application/AiAssistant/KnowledgeIndexingService.cs`, `tests/TalentPilot.Tests/AiAssistant/KnowledgeIndexingServiceTests.cs`, `scripts/migrations/038_sanitize_bench_match_experience_rationale.sql`, contributor log.
- Endpoints changed: no.
- Schema changed: no schema shape change; added idempotent data migration to sanitize existing bench-matching recommendations, knowledge chunks, and saved citation excerpts.
- Seed/stored procedures changed: no seed or stored procedure changes.
- Tests run: `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --filter "BenchMatchingAgentTests|KnowledgeIndexingServiceTests"` passed with 10 tests; database script runner completed successfully; direct SQL counters confirmed zero stale recommendation, chunk, and citation rows.
- Known risks: existing browser sessions may need a page reload to display the corrected saved citation excerpt.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-06 - Branch: mudasar-ahmad

- Commit summary: pending job request client context persistence and AI prompts.
- Purpose: persist optional client context on job requests and pass it into job-description drafting, request embeddings, bench matching, applicant ranking, talent rediscovery, and online headhunting enrichment so AI agents can use tenant-provided industry/client signals.
- Files touched: operations DTO/service/repository, AI contracts and agents, SQL schema/view/migration scripts, AI tests, knowledge-base docs, contributor log.
- Endpoints changed: existing job request create/list/detail responses and draft job description input now include optional `clientContext`.
- Schema changed: added nullable `JobRequests.ClientContext`, updated `vw_JobRequestDashboard`, and added idempotent migration `037_add_job_request_client_context.sql`.
- Seed/stored procedures changed: no seed or stored procedure changes.
- Tests run: targeted `dotnet test tests\TalentPilot.Tests\TalentPilot.Tests.csproj --filter "JobDescriptionDraftingAgentTests|BenchMatchingAgentTests|TalentRediscoveryAgentTests|ApplicantRankingAgentTests|OnlineHeadhuntingAgentTests|KnowledgeIndexingServiceTests"` passed with 27 tests; database script runner completed successfully against the local Talent Pilot connection; API `/health` returned 200 after restart.
- Known risks: client context is not an automatic external web lookup; agents use it as supplied context unless the text explicitly indicates live/recent web research is needed.
- AI assistance: Codex implemented and reviewed the change.

## 2026-06-04 - Branch: mudasar-ahmad

- Commit summary: pending AI interview question recommender.
- Purpose: add an LLM-backed Interview Question Recommender that retrieves seeded question-bank evidence, generates structured interviewer question sets, persists version history per interview, and exports the latest set as DOCX.
- Files touched: `src/TalentPilot.Api/Controllers/OperationsController.cs`, application AI/operations/document contracts and service, Dapper operations repository, OpenXML export service, SQL migration/seed scripts, AI/export tests, knowledge-base docs, contributor log.
- Endpoints changed: added `GET /api/talent-pilot/interviews/{interviewId}/question-recommendations`, `POST /api/talent-pilot/interviews/{interviewId}/question-recommendations/generate`, and `GET /api/talent-pilot/interviews/{interviewId}/question-recommendations/download`.
- Schema changed: added `InterviewQuestionBankItems`, `InterviewQuestionRecommendationSets`, and `InterviewQuestionRecommendations`; registered `interview-question-recommender`.
- Seed/stored procedures changed: added seeded per-skill and generic question-bank items for every active tenant skill; no stored procedure changes.
- Follow-up fix: enabled Ollama JSON mode for structured-output requests, requires at least 10 generated questions, increased the backend AI HTTP timeout, added DOCX export, and allowed visible completed interview tasks whose application row is inactive to generate recommendations.
- Tests run: `dotnet test` passed with 86 tests; database script runner completed successfully against local `TalentPilot`; verified Amara regeneration persisted version 2 with 10 questions and DOCX package download.
- Known risks: Local `llama3.2` generation is still slow; the verified Amara 10-question regeneration took about 473 seconds.
- AI assistance: Codex implemented and reviewed the changes.

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
