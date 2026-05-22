# Database Schema

The executable SQL source of truth is `Application Code/Backend Code/scripts`.

The broader design discussion lives in `../Database/Schema` and `../How workflows work`. Treat those as reference material, not executable backend migration scripts.

## Script Ownership

- `scripts/schema/001_create_tables.sql`
  - tenants, recruitment settings, users, credentials, refresh tokens, roles, permissions, groups, tenant access policies, notification events/templates/outbox, audit logs, AI definitions/runs, vector embeddings.
- `scripts/schema/002_create_domain_tables.sql`
  - AI runtime settings, role assignment batches, departments, locations, skills, source labels, projects, employees, candidates, job requests, applications, referrals, fulfillments, interviews, workflows, recipients, recommendations.
- `scripts/schema/003_create_views.sql`
  - public jobs, active workflow assignments, dashboard summaries, bench availability.
- `scripts/seed/001_seed_initial_data.sql`
  - TKXEL tenant, demo users with BCrypt `demo` password hashes, roles, permissions, groups, notifications, AI agents.
- `scripts/seed/002_seed_domain_reference_data.sql`
  - departments, locations, skills, employees, candidate/application sample data, workflow routing, interview template, AI runtime.
- `scripts/stored-procedures/001_user_procedures.sql`
  - user context, admin user list/detail/update procedures.
- `scripts/stored-procedures/002_workflow_and_candidate_procedures.sql`
  - workflow assignment claim, PMO assignment notification trigger, and candidate re-apply checks.

## Schema Domains And Why They Exist

- Tenant configuration
  - `Tenants`, `TenantRecruitmentSettings`, `TenantAiSettings`, `TenantAccessPolicies`
  - Stores tenant identity, status, timezone, currency, career page settings, permission policy, and runtime model values.
- Identity and authorization
  - `AppUsers`, `UserCredentials`, `RefreshTokens`, `Roles`, `Permissions`, `RolePermissions`, `UserRoles`
  - Builds auth profile, role display, role priority, and effective permission ids.
- Workflow routing groups
  - `Groups`, `GroupMembers`
  - Routes work to PMO, recruiter, hiring manager, and interviewer groups. Groups do not grant permissions.
- Organization and bench data
  - `Departments`, `Locations`, `Skills`, `Projects`, `Employees`, `EmployeeSkills`, `EmployeeProjectAssignments`
  - Supports PMO bench matching and internal resource recommendations.
- Job request lifecycle
  - `JobRequests`, `JobRequestSkills`, `JobRequestEmployeeReferrals`, `JobRequestFulfillments`
  - Tracks the resource need, required positions, internal referrals, and fulfillment.
- Candidate sourcing and application
  - `CandidateSourceLabels`, `Candidates`, `CandidateDocuments`, `CandidateSkills`, `CandidateProspects`, `CandidateProspectJobRequests`, `CandidateInvitations`, `JobApplications`, `JobApplicationStatusHistory`, `CandidateEmployeeLinks`
  - Keeps sourced prospects, registered candidates, job-specific applications, invite tokens, re-apply history, and hired-candidate-to-employee traceability.
- Hiring pipeline and interviews
  - `InterviewTemplates`, `InterviewTemplateRounds`, `JobRequestInterviewRounds`, `Interviews`, `InterviewFeedback`
  - Stores fixed job-post pipeline templates, interview assignments, and feedback.
- Workflows and baton movement
  - `WorkflowDefinitions`, `WorkflowStages`, `WorkflowTransitions`, `WorkflowRoutingRules`, `WorkflowActionPermissions`, `WorkflowAssignments`, `WorkflowHistory`
  - Controls operational handoffs such as Presales to PMO, PMO to Recruiter, and Hiring Manager routing.
- Notifications and audit
  - `NotificationEvents`, `NotificationTemplates`, `NotificationRecipients`, `NotificationOutbox`, `AuditLogs`
  - Stores durable notification records, email template text, SignalR/email outbox rows, and audit history.
- AI and vector search
  - `AiAgentDefinitions`, `AiAgentRuns`, `AiRecommendationLogs`, `VectorEmbeddings`
  - Stores advisory AI execution traces, recommendation explanations, model metadata, source hashes, and 768-dimensional embeddings.

## Persistence Rules

- Every tenant-owned table must include `TenantId`.
- Store all timestamps as UTC `DATETIME2(3)` columns with `Utc` suffixes.
- Return UTC ISO values to frontend; frontend formats local display.
- Tenant timezone is stored as an IANA id, for example `Asia/Karachi`.
- Tenant currency is stored as an ISO 4217 code, for example `PKR`.
- Tenant status is `Active` or `Inactive`.
- User list displays highest-priority role from `Roles.Priority`; full assignments remain in `UserRoles`.
- Permission conflict behavior is stored in tenant access policies.
- Bulk role assignment writes role rows plus `RoleAssignmentBatches` for auditability.
- Candidate invite tokens store hashes only, never raw tokens.
- Demo internal and candidate users are seeded with uppercase `EmailNormalized` values and a BCrypt hash for shared password `demo`.
- Re-apply checks use final decision timestamp plus configured cooldown.
- Notification delivery is code-owned. Schema stores events, recipients, templates, and outbox work.
- Vector embeddings must always be tenant-filtered and model/dimension-aware. `VectorEmbeddings` is created only on SQL Server major version 17 or later because it uses native `VECTOR(768)`.

## SQL Safety Rules

- Scripts must be idempotent.
- Prefer additive changes.
- Prefer `CREATE OR ALTER PROCEDURE` and `CREATE OR ALTER TRIGGER`.
- Do not drop seeded MVP data without explicit approval.
- Keep seed data separate from schema.
- Keep stored procedures separate from seed files.
