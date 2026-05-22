# Backend And Database Agent Guide

This repository is the .NET backend, SQL script, worker, and test repository for Talent Pilot. AI agents working here must keep API behavior auditable, tenant-scoped, and documented.

## Read First

- `README.md`
- `SECURITY_GUIDELINES.md`
- `CONTRIBUTING.md`
- `knowledge-base/README.md`
- `knowledge-base/api-surface.md`
- `knowledge-base/backend-data-contracts.md`
- `knowledge-base/business-rules.md`
- `knowledge-base/database-schema.md`
- `scripts/README.md`
- `scripts/migrations/README.md`

## Folder Map

| Path | Purpose |
| --- | --- |
| `src/TalentPilot.Api/` | Controllers, middleware, auth accessor, startup. |
| `src/TalentPilot.Application/` | Use cases, DTOs, service interfaces, validation boundaries. |
| `src/TalentPilot.Domain/` | Domain constants and small policy helpers. |
| `src/TalentPilot.Infrastructure/` | Dapper repositories, SQL connection, tokens, runtime integrations. |
| `src/TalentPilot.Database/` | Idempotent SQL script runner. |
| `src/TalentPilot.Worker/` | Local background worker for notification outbox. |
| `scripts/schema/` | Idempotent table/index/schema scripts. |
| `scripts/seed/` | Idempotent lookup and MVP demo seed scripts. |
| `scripts/stored-procedures/` | Idempotent stored procedure scripts. |
| `scripts/migrations/` | Migration notes and task files. |
| `tests/TalentPilot.Tests/` | xUnit tests. |
| `contributors/` | Required contributor session logs. |

## Architecture Flow

1. Controller receives HTTP request and stays thin.
2. Application service validates and orchestrates use cases.
3. Repository handles Dapper/SQL persistence.
4. Domain constants/policies keep shared business terms explicit.
5. DTOs define the API contract consumed by Angular.
6. Tests cover application policies and important API/service behavior.

Do not put SQL access in controllers. Do not return database rows directly as API contracts.

## Database And Migration Flow

The script runner executes:

1. `scripts/schema/*.sql`
2. `scripts/seed/*.sql`
3. `scripts/stored-procedures/*.sql`

Rules:

- Every SQL script must be idempotent.
- Schema scripts create/alter tables, indexes, constraints, and vector columns.
- Seed scripts insert/update lookup rows, roles, permissions, demo users, workflow definitions, and MVP sample data.
- Stored procedure scripts create/alter procedures used by workflow and candidate operations.
- Migration notes describe why a change exists and what to verify.
- Do not mix schema, seed, and stored procedure changes in the same script file.
- Update `knowledge-base/database-schema.md` for table/relationship changes.

Run the database runner after SQL changes:

```powershell
dotnet run --project src/TalentPilot.Database -- --connection "<connection-string>"
```

## Working With Frontend Agents

When you add or change an API:

1. Update DTOs and service interfaces.
2. Add or update controller action.
3. Update Dapper and in-memory repositories if both are still used by tests/local fallback.
4. Update `knowledge-base/api-surface.md`.
5. Add or update tests.
6. Tell the frontend agent the endpoint path, method, request body, response shape, permissions, and error cases.

Do not change Angular files from this repo unless explicitly assigned cross-repo integration work.

## Workflow And Notification Rules

- Workflow handoffs are operational state, not admin-only UI decoration.
- Roles grant permissions; groups route workflow work.
- Notification delivery is backend-owned. Email and SignalR are triggered by backend code.
- Admin Center may manage notification templates/events, but not per-row transport behavior for workflow actions.
- Use outbox records for reliable background delivery.
- Keep every workflow decision auditable.

## AI And Vector Rules

- AI is advisory only.
- Do not add auto-reject, auto-hire, or final decision automation.
- Keep model names and embedding dimensions consistent with configured runtime.
- If embedding model/dimensions change, document re-indexing requirements.
- Prefer local/free runtime boundaries for MVP.

## Typical Backend Agent Scopes

- API agent: controllers, DTOs, application services, tests.
- Repository agent: Dapper queries, in-memory parity, persistence tests.
- Database agent: schema/seed/procedure scripts and database docs.
- Worker agent: outbox processing and background service behavior.
- Auth agent: auth/session/permission resolution and tests.

## Git And Push Rules

- Never work directly on `main`.
- Every contributor and every AI-assisted contributor must work on their own branch.
- Branch ownership matters: do not reuse another contributor's branch unless the code owner explicitly assigns you to that branch.
- Do not push to GitHub automatically after making changes.
- Push only when Mudasar Ahmad or the current user explicitly asks you to push in that session.
- When a push is requested, push only your scoped branch and never push directly to `main`.
- Do not stage unrelated files, generated outputs, secrets, or code generated by another contributor.

## Validation

Run:

```powershell
dotnet test
```

If SQL changed, also run the database script runner and document the connection target generically without exposing secrets.

## Finish Checklist

- Branch is not `main`.
- Branch belongs to the current contributor/session.
- `dotnet test` passes.
- SQL runner executed if scripts changed, or the reason it was not run is documented.
- API/schema docs updated.
- Contributor log updated in `contributors/<contributor-name>/README.md`.
- Frontend contract impact is documented for frontend agents.
- GitHub push performed only if explicitly requested by the user.
