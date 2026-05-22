# Backend Security Guidelines

These rules apply to every human or AI-assisted contributor working on the Talent Pilot backend.

## Core Rules

- Do not commit secrets, real connection strings, JWT signing keys, API keys, passwords, local appsettings files, logs, `bin`, `obj`, or `TestResults`.
- Do not hard-code tenant ids, user ids, role ids, permission ids, credentials, or tokens in application logic.
- Do not add paid infrastructure, paid queues, paid AI APIs, hosted SaaS dependencies, or closed-source packages without code owner approval.
- Keep tenant-scoped data filtered by `TenantId`.
- Store persisted timestamps as UTC `DATETIME2(3)` and return ISO UTC timestamps to the frontend.
- Do not expose schema names, SQL errors, stack traces, token internals, or implementation details in API responses.

## Authentication And Authorization

- Passwords must be stored as secure hashes, never plaintext.
- Keep auth/session endpoints separate from Admin Center user management.
- Authorize commands by permission ids, not display role names.
- Roles grant permissions. Groups route workflow work. Do not merge those concepts.
- Refresh-token behavior must be auditable and revocable.

## SQL And Persistence

- SQL scripts must be idempotent.
- Keep schema, seed data, stored procedures, and migration task files separated.
- Use parameterized Dapper queries. Do not concatenate user input into SQL.
- Update `knowledge-base/database-schema.md` when schema, seed, or stored procedure behavior changes.
- Run the database script runner after schema changes and document the result.

## Workflow, Notifications, And AI

- Workflow handoffs must be auditable through assignments/history.
- Notification delivery is backend-owned. Email and SignalR behavior is implemented in code, not configured per workflow row by admins.
- Use the SQL outbox/local worker for MVP background delivery. Do not introduce paid queues.
- AI output is advisory only. AI must not auto-reject, auto-hire, or make final recruitment decisions.

## AI-Assisted Contribution Rules

- AI-generated backend code must be reviewed by a contributor before PR submission.
- AI must not invent schema fields, enum values, or endpoints without updating scripts, knowledge-base docs, and tests.
- AI must not bypass authorization checks, tenant filters, or audit requirements to make a demo path work.
- AI must update `contributors/<contributor-name>/README.md` with files touched, tests run, and risks.

## Required Checks Before PR

- Run `dotnet test`.
- Run the database script runner if SQL scripts changed.
- Confirm no local secrets or generated build outputs are staged.
- Update backend knowledge-base docs and contributor logs.
