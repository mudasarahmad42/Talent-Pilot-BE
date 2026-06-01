# Talent Pilot Backend

.NET 8 Clean Architecture backend for the Talent Pilot TKXEL AI Unlimited MVP.

The backend owns authentication, authorization context, Admin Center APIs, Talent Pilot operational APIs, SQL persistence, workflow handoffs, notification records, audit, and future AI/background processing.

For cross-system request, email, realtime notification, worker, and AI flow diagrams, keep the root workspace architecture reference updated: `../../ARCHITECTURE.md`.

## Stack

- .NET 8
- ASP.NET Core Web API
- Dapper
- Microsoft.Data.SqlClient
- SQL Server 2025 Developer / SQLEXPRESS
- SQL Server `VECTOR(768)`
- JWT + refresh tokens
- BCrypt password verification
- SignalR-ready notification model
- SQL-backed outbox with local worker
- xUnit tests

## Dependency Policy

- Prefer free, open-source, well-maintained libraries.
- Do not add paid SaaS services, hosted queues, paid AI APIs, or closed-source packages without team approval.
- Use Dapper and SQL scripts for persistence.
- Do not introduce Entity Framework unless the team explicitly changes the persistence strategy.
- Keep background processing simple through the SQL outbox and local worker for MVP.
- Keep AI runtime local/free by default: mock/Ollama-compatible runtime, `llama3.2`, and `nomic-embed-text`.

## Prerequisites

- .NET SDK `8.x`
- SQL Server 2025 Developer or SQLEXPRESS compatible with `VECTOR(768)`
- PowerShell
- Optional: Ollama-compatible local AI runtime at `TalentPilotRuntime:OllamaBaseUrl` for the Job Description Drafting Agent and description embeddings

## Solution Structure

```text
src/
  TalentPilot.Api/             Controllers, middleware, API startup
  TalentPilot.Application/     Use cases, DTOs, contracts, validation boundaries
  TalentPilot.Domain/          Domain constants and small policy helpers
  TalentPilot.Infrastructure/  Dapper repositories, SQL connection factory, auth/token services
  TalentPilot.Database/        Idempotent SQL script runner
  TalentPilot.Worker/          BackgroundService worker for SQL outbox processing
  TalentPilot.Common/          Shared primitives such as time abstractions
tests/
  TalentPilot.Tests/
scripts/
  schema/
  seed/
  stored-procedures/
  migrations/
```

## Architecture Rules

- API controllers stay thin.
- Application layer owns use-case orchestration and DTO contracts.
- Infrastructure owns SQL, Dapper, token, and runtime integrations.
- Domain stays small and explicit for MVP.
- Use Dapper and plain SQL for persistence.
- Keep SQL tenant-scoped through `TenantId`.
- Do not put endpoint names or schema explanations in UI responses.
- Keep roles and groups separate:
  - roles grant permissions
  - groups route workflow work
- Follow SOLID principles pragmatically. Keep classes and methods focused, inject real dependencies, and avoid abstractions that do not simplify a concrete use case.
- Do not overcomplicate MVP slices. Prefer a clear vertical path through controller, service, repository, SQL, and tests over generic frameworks or speculative extension points.

## Backend Guardrails

- Keep business behavior auditable.
- AI agents must read `AGENTS.md` before editing backend, worker, test, or SQL files.
- Store all persisted timestamps as UTC `DATETIME2(3)` values.
- Return ISO UTC timestamps to the frontend.
- Keep tenant-scoped tables keyed by `TenantId`.
- Keep auth/session endpoints separate from Admin Center user/role/group management.
- Notifications are backend-owned. Email and SignalR are triggered by backend code, not user-configured per workflow row.
- AI recommendations are advisory only; humans make final PMO, recruiter, interviewer, and hiring-manager decisions.
- Keep workflow handoffs separate from candidate interview pipeline stages.
- Do not expose schema notes, endpoint names, or internal technical labels in product UI responses.
- Follow `SECURITY_GUIDELINES.md` before changing authentication, authorization, SQL, file upload, background workers, integrations, or AI runtime code.

## Local Database

Connection key:

```text
ConnectionStrings:TalentPilot
```

Local development target:

```text
Server=TK-LPT-1286\SQLEXPRESS;Database=TalentPilot;User ID=sa;Password=<local-password>;Encrypt=False;TrustServerCertificate=True;Connection Timeout=15;
```

Do not commit passwords. Use environment variables or user-secrets.

## Setup From A Fresh Clone

```powershell
git clone https://github.com/mudasarahmad42/Talent-Pilot-BE.git
cd Talent-Pilot-BE
dotnet restore
```

## Branch And PR Policy

- Do not work directly on `main`.
- Only the code owner, Mudasar Ahmad, is allowed to commit or push directly to `main`.
- Every contributor, including AI-assisted contributors, must create their own separate branch and open a pull request into `main`.
- Contributors and AI agents must not push automatically after making changes; push only when Mudasar Ahmad or the current user explicitly asks for it.
- When a push is explicitly requested, push only the contributor's own branch and only the files that belong to the requested task.
- GitHub branch protection should block direct pushes to `main` for everyone except the code owner.
- This repo includes `.githooks/pre-push`; run `git config core.hooksPath .githooks` after cloning to block accidental local pushes to `main`.
- Use descriptive owner-aware branch names such as `feature/<contributor-name>/workflow-claim-api`, `fix/<contributor-name>/auth-token-refresh`, `schema/<contributor-name>/job-request-fulfillment`, or `docs/<contributor-name>/contributor-guardrails`.
- Keep pull requests focused. Do not mix unrelated schema, API, worker, test, and documentation changes in one PR unless they are required for the same feature.
- PRs must include validation notes, endpoints changed, SQL scripts changed, files touched, and any known migration/runtime risks.
- Do not merge your own PR unless you are the code owner or have explicit approval from the code owner.
- If an AI tool generated or edited code, the contributor remains responsible for reviewing, testing, and documenting the changes.
- See `CONTRIBUTING.md` for branch protection, PR, and merge-conflict rules.

## Contributor Logs

- Each contributor must add or update a personal README under `contributors/<contributor-name>/README.md`.
- The contributor README should be 10-20 lines per work session.
- Include session date, branch name, commit summary, files touched, endpoints changed, schema changes, seed changes, stored procedures changed, tests run, and known risks.
- If backend changes affect the frontend, link the related frontend PR or document the dependency clearly.
- Contributors who are non-technical or AI-assisted should use `contributors/README.md` as the template.
- Missing contributor logs are a PR review issue.

PowerShell environment variable example:

```powershell
$env:ConnectionStrings__TalentPilot = "Server=TK-LPT-1286\SQLEXPRESS;Database=TalentPilot;User ID=sa;Password=<local-password>;Encrypt=False;TrustServerCertificate=True;Connection Timeout=15;"
```

User-secrets example:

```powershell
dotnet user-secrets set "ConnectionStrings:TalentPilot" "<connection-string>" --project src/TalentPilot.Api
dotnet user-secrets set "ConnectionStrings:TalentPilot" "<connection-string>" --project src/TalentPilot.Worker
dotnet user-secrets set "DataAccess:IdentityProvider" "SqlServer" --project src/TalentPilot.Api
```

## AI Web Search

The Bench Matching Agent can enrich PMO explanations with public Tavily web research context when a request needs recent/live client, market, industry, regulatory, or news context. The backend calls Tavily from the code-owned AI agent, so searches remain auditable and quota-controlled.

Store the Tavily API key in user-secrets or deployment secrets:

```powershell
dotnet user-secrets set "TavilySearch:ApiKey" "<tavily-api-key>" --project src/TalentPilot.Api
```

`TavilySearch:DailyRequestLimit` is capped at 60 requests per UTC day in code and persisted through `dbo.ExternalToolDailyUsage`.

Talent Rediscovery intentionally does not use web search. It ranks only tenant-owned candidate history, outcomes, interview feedback, skills, and vectors because candidate personal identifiers must never be searched externally.

## Run Database Scripts

Create the empty `TalentPilot` database first if it does not exist, then run:

```powershell
dotnet run --project src/TalentPilot.Database -- --connection "<connection-string>"
```

Execution order:

1. `scripts/schema/*.sql`
2. `scripts/migrations/*.sql`
3. `scripts/seed/*.sql`
4. `scripts/stored-procedures/*.sql`

Scripts are additive and idempotent.

## Database Migration Policy

We maintain plain SQL migrations under `scripts/migrations/` so teammates can keep existing databases current without recreating local data.

- Put current-state table/view definitions in `scripts/schema/`.
- Put one-off, idempotent upgrade/data-fix scripts in `scripts/migrations/`.
- Put demo/reference tenant data in `scripts/seed/`.
- Put `CREATE OR ALTER PROCEDURE` files in `scripts/stored-procedures/`.
- Name new migration files with the next numeric prefix, for example `002_add_candidate_offer_tracking.sql`.
- Every migration must be safe to rerun. Use existence checks, `MERGE`, guarded `ALTER TABLE`, or targeted `UPDATE` statements.
- Do not edit old migration files after they have been shared unless the file has never been run by anyone else.
- If schema, seed, or stored procedure files changed, run the database runner before testing the API.

Schema, seed, and stored procedure intent is documented in:

```text
knowledge-base/database-schema.md
scripts/README.md
scripts/migrations/README.md
```

## Run API

```powershell
dotnet run --project src/TalentPilot.Api
```

Default local HTTP profile:

```text
http://localhost:5058
```

Health endpoint:

```text
GET /health
```

## Run Worker

```powershell
dotnet run --project src/TalentPilot.Worker
```

The worker processes SQL-backed outbox records locally. Keep this simple for MVP; do not introduce paid queues unless explicitly approved.

Configure the worker with the same `ConnectionStrings:TalentPilot` value as the API. Workflow email delivery also needs `Resend:ApiKey` and optionally `Resend:FromEmail` from user-secrets or deployment secrets. Pending rows are marked `Sent` after provider delivery succeeds, or `Failed` with `LastError` when the provider rejects the message.

## Test

```powershell
dotnet test
```

If the API is running and locks build outputs, stop the API before running a full rebuild.

## Current Implemented API Groups

- `api/auth/*`
- `api/admin/tenant-profile`
- `api/admin/users`
- `api/admin/roles`
- `api/admin/groups`
- `api/admin/access-policies/*`
- `api/admin/notifications/*`
- `api/admin/ai-settings/*`
- `api/admin/integrations/status`
- `api/admin/audit-logs`
- `api/talent-pilot/*`

Detailed endpoint notes are in `knowledge-base/api-surface.md`.

## Current Persistence State

SQL mode is enabled through:

```text
DataAccess:IdentityProvider=SqlServer
```

When SQL mode is enabled, the backend uses Dapper repositories for:

- authentication and current user context
- tenant profile
- Admin Center users
- Admin Center roles and access policies
- groups
- notifications and notification templates
- AI runtime/settings
- audit logs
- Talent Pilot operations snapshot
- workflow assignment claim
- notification read/read-all
- notification outbox processing

In-memory repositories remain only as local fallback/testing when SQL mode is disabled.

## Knowledge Base

Read these before changing backend behavior:

- `knowledge-base/README.md`
- `knowledge-base/authentication.md`
- `knowledge-base/api-surface.md`
- `knowledge-base/backend-data-contracts.md`
- `knowledge-base/business-rules.md`
- `knowledge-base/database-schema.md`
- `knowledge-base/implemented-vs-planned.md`
- `CONTRIBUTING.md`

## Production Readiness Notes

- Replace the development JWT signing key before any non-local deployment.
- Demo role cards use the normal email/password auth endpoint with seeded BCrypt password hashes.
- Store real credentials as BCrypt hashes in `UserCredentials`.
- Store all persisted timestamps in UTC.
- Return ISO UTC timestamps to frontend.
- Let frontend convert to user/tenant local time.
- Notification delivery is backend-owned: Email and SignalR.
- AI recommendations remain advisory and auditable.

## Before Opening A PR

- Run `dotnet test`.
- Run the database script runner if schema, seed, or stored procedure files changed.
- Update `knowledge-base/` when changing endpoints, schema, workflow behavior, auth, permissions, notifications, or persistence.
- Update your contributor log in `contributors/<contributor-name>/README.md`.
- Do not commit `bin`, `obj`, `TestResults`, logs, local secrets, local appsettings files, or real connection strings.
