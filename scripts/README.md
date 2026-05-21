# SQL Scripts

Executable SQL scripts for the Talent Pilot backend database.

The database runner processes folders in this order:

1. `schema/*.sql`
2. `seed/*.sql`
3. `stored-procedures/*.sql`

Within each folder, files are sorted by filename.

## Rules

- Scripts must be idempotent.
- Prefer additive changes.
- Keep schema, seed data, and stored procedures separate.
- Do not commit local credentials.
- Do not drop data unless explicitly approved.
- Use `TenantId` on tenant-owned business tables.
- Store persisted timestamps as UTC `DATETIME2(3)` columns with `Utc` suffixes.
- Use `CREATE OR ALTER PROCEDURE` for stored procedures.

## Source Of Truth

These scripts are the executable source of truth for the backend database.

The broader design notes in `../../Database/Schema` and `../../How workflows work` explain why the schema exists and how it maps to the recruitment workflow.
