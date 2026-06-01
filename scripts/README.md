# SQL Scripts

Executable SQL scripts for the Talent Pilot backend database.

The database runner processes folders in this order:

1. `schema/*.sql`
2. `migrations/*.sql`
3. `seed/*.sql`
4. `stored-procedures/*.sql`

Within each folder, files are sorted by filename.

## Dev-Only Scripts

`dev/*.sql` scripts are not part of the normal database runner. Run them manually only for local smoke testing.

- `dev/set_candidate_test_emails.sql` drops only the `Candidates.Email` uniqueness constraint, backs up existing candidate emails, and sets candidate contact emails to `mudasar.ahmad@tkxel.com` for email-delivery testing.
- `dev/revert_candidate_test_emails.sql` restores the backed-up candidate emails and recreates the candidate uniqueness constraint when the restored data has no duplicates.

Do not remove the `AppUsers.Email` uniqueness constraint. Application login resolves users by app-user email.

## Rules

- Scripts must be idempotent.
- Prefer additive changes.
- Keep schema, seed data, and stored procedures separate.
- Use `migrations/` for idempotent upgrade scripts that existing developer databases need.
- Do not commit local credentials.
- Do not drop data unless explicitly approved.
- Use `TenantId` on tenant-owned business tables.
- Store persisted timestamps as UTC `DATETIME2(3)` columns with `Utc` suffixes.
- Use `CREATE OR ALTER PROCEDURE` for stored procedures.

## Source Of Truth

These scripts are the executable source of truth for the backend database.

The broader design notes in `../../Database/Schema` and `../../How workflows work` explain why the schema exists and how it maps to the recruitment workflow.
