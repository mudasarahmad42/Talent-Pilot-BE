# Talent Pilot Database Setup

## Local SQL Server Target

The current local development database is:

```text
Server: TK-LPT-1286\SQLEXPRESS
Database: TalentPilot
Authentication: SQL Authentication
SQL Server major version: 17
Vector support: native SQL Server vector type is available
```

Do not commit SQL credentials. Store the connection string in .NET user-secrets or an environment variable.

Connection string shape:

```text
Server=TK-LPT-1286\SQLEXPRESS;Database=TalentPilot;User ID=sa;Password=<local-password>;Encrypt=False;TrustServerCertificate=True;Connection Timeout=15;
```

`Encrypt=False` is currently required for the local SQLEXPRESS instance because `sqlcmd` fails the ODBC encryption handshake on this machine. The backend runner uses `Microsoft.Data.SqlClient` and connects successfully with this setting.

## What Was Applied

The database `TalentPilot` is created through the SQL runner. The backend scripts now include the executable version of the wider `Database/Schema` discussion package:

1. `scripts/schema/001_create_tables.sql`
2. `scripts/schema/002_create_domain_tables.sql`
3. `scripts/schema/003_create_views.sql`
4. `scripts/seed/001_seed_initial_data.sql`
5. `scripts/seed/002_seed_domain_reference_data.sql`
6. `scripts/stored-procedures/001_user_procedures.sql`
7. `scripts/stored-procedures/002_workflow_and_candidate_procedures.sql`

The scripts are additive and idempotent. The design discussion scripts under `../Database/Schema` are reference material only; they use earlier naming conventions and should not be run directly against this backend database.

Current sanity counts after seed:

| Object | Count |
| --- | ---: |
| Tenants | 1 |
| AppUsers | 7 |
| Roles | 8 |
| Groups | 3 |
| Permissions | 14 |
| Departments | 6 |
| Skills | 9 |
| Employees | 6 |
| JobRequests | 1 |
| WorkflowAssignments | 1 |
| Candidates | 1 |
| CandidateSourceLabels | 4 |
| InterviewTemplates | 1 |
| NotificationEvents | 7 |
| AiAgentDefinitions | 6 |
| Stored procedures | 6 |

## API And Worker Configuration

The API and worker use the configuration key:

```text
ConnectionStrings:TalentPilot
```

For local development this value is stored in .NET user-secrets, not in `appsettings.json`.

The API can also use SQL-backed auth/session identity by setting:

```text
DataAccess:IdentityProvider=SqlServer
```

When this is set, authentication reads users, roles, permissions, groups, refresh tokens, and `LastActiveAtUtc` through `DapperIdentityRepository`.

Admin Center, Talent Pilot operations, audit, notifications, and outbox processing are also SQL-backed in this mode through Dapper repositories. In-memory repositories remain only as a local fallback/testing path when SQL mode is disabled.

## Runtime Rules For Backend And Database Agents

- Store timestamps in UTC using `DATETIME2(3)` columns with `Utc` suffixes.
- Convert UTC timestamps to tenant/user local time on the client.
- Store tenant status as `Tenants.Status` with allowed values `Active` and `Inactive`.
- Store tenant timezone as an IANA timezone ID such as `Asia/Karachi` in `Tenants.DefaultTimezoneId`.
- Store tenant currency as ISO 4217 code in `Tenants.DefaultCurrencyCode`.
- Roles grant permissions.
- Groups route workflow work only.
- Notification channels are backend-owned: `Email` and `SignalR`.
- AI decisions are advisory. Do not persist automatic rejection, automatic offer, or final hiring decision automation as MVP behavior.
- Vector embeddings use `VECTOR(768)` for the configured `nomic-embed-text` model and must always be tenant-scoped.
- The expanded domain model is documented in [knowledge-base/database-schema.md](knowledge-base/database-schema.md).
