# Talent Pilot SQL Script Conventions

`TalentPilot.Database` runs plain SQL files in this order:

1. `scripts/schema/*.sql`
2. `scripts/seed/*.sql`
3. `scripts/stored-procedures/*.sql`

Files are sorted by name inside each folder. Use numeric prefixes such as `001_`, `002_`, and keep every script re-runnable.

## Current MVP Scripts

- `schema/001_create_tables.sql` creates tenant, access control, notification, audit, AI agent, and SQL Server 2025 `VECTOR(768)` tables. Vector DDL is guarded by SQL Server major version 17.
- `schema/002_create_domain_tables.sql` creates domain, workflow, interview, and recommendation tables.
- `schema/003_create_views.sql` creates public job, workflow assignment, job request dashboard, and bench availability views.
- `seed/001_seed_initial_data.sql` seeds the TKXEL demo tenant, system roles, permissions, routing groups, notification events/templates, AI agent definitions, and demo users with BCrypt hashes for password `demo`.
- `seed/002_seed_domain_reference_data.sql` seeds departments, locations, skills, employees, a sample job request, workflow routing, interview templates, AI settings, and initial PMO notification/outbox records.
- `stored-procedures/001_user_procedures.sql` creates auth/admin user helper procedures.
- `stored-procedures/002_workflow_and_candidate_procedures.sql` creates workflow claim, PMO assignment notification trigger, and candidate re-apply helpers.

## Migration Rules

- Use UTC timestamp columns with a `Utc` suffix, normally `DATETIME2(3)` with `SYSUTCDATETIME()`.
- Put `TenantId` on tenant-owned tables and filter by it in procedures.
- Keep groups for workflow routing only. Permissions come from roles.
- Prefer `CREATE OR ALTER PROCEDURE` for stored procedures and `CREATE OR ALTER TRIGGER` for triggers.
- For additive schema changes, create a new numbered script. Do not rewrite historical scripts after teammates have run them.
- For destructive changes, document the data impact in the script header before making the change.
