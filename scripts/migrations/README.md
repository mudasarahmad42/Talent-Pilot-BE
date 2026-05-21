# Talent Pilot SQL Script Conventions

`TalentPilot.Database` runs plain SQL files in this order:

1. `scripts/schema/*.sql`
2. `scripts/seed/*.sql`
3. `scripts/stored-procedures/*.sql`

Files are sorted by name inside each folder. Use numeric prefixes such as `001_`, `002_`, and keep every script re-runnable.

## Current MVP Scripts

- `schema/001_create_tables.sql` creates tenant, access control, notification, audit, AI agent, and vector tables.
- `seed/001_seed_initial_data.sql` seeds the TKXEL demo tenant, system roles, permissions, routing groups, notification events/templates, AI agent definitions, and demo users.
- `stored-procedures/001_user_procedures.sql` creates simple auth/admin user helper procedures.

## Migration Rules

- Use UTC timestamp columns with a `Utc` suffix, normally `DATETIME2(3)` with `SYSUTCDATETIME()`.
- Put `TenantId` on tenant-owned tables and filter by it in procedures.
- Keep groups for workflow routing only. Permissions come from roles.
- Prefer `CREATE OR ALTER PROCEDURE` for stored procedures.
- For additive schema changes, create a new numbered script. Do not rewrite historical scripts after teammates have run them.
- For destructive changes, document the data impact in the script header before making the change.
