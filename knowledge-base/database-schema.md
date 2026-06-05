# Database Schema

The executable SQL source of truth is `Application Code/Backend Code/scripts`.

The broader design discussion lives in `../Database/Schema` and `../How workflows work`. Treat those as reference material, not executable backend migration scripts.

Canonical business source of truth: [../../../TALENT_PILOT_SOURCE_OF_TRUTH.md](../../../TALENT_PILOT_SOURCE_OF_TRUTH.md)

## Script Ownership

- `scripts/schema/001_create_tables.sql`
  - tenants, recruitment settings, users, credentials, refresh tokens, roles, permissions, groups, tenant access policies, notification events/templates/outbox, audit logs, AI definitions/runs, vector embeddings.
- `scripts/schema/002_create_domain_tables.sql`
  - AI runtime settings, role assignment batches, departments, locations, skills, source labels, projects, employees, candidates, job requests, applications, referrals, fulfillments, interviews, workflows, recipients, recommendations.
- `scripts/schema/003_create_views.sql`
  - public jobs, active workflow assignments, dashboard summaries, bench availability.
- `scripts/schema/004_add_tenant_logo.sql`
  - tenant branding logo file name, content type, and binary payload on recruitment settings.
- `scripts/schema/005_normalize_roles_model.sql`
  - keeps System Admin as the only platform/system role and normalizes seeded tenant roles to tenant-scoped, editable role records.
- `scripts/seed/001_seed_initial_data.sql`
  - TKXEL tenant, demo users, roles, permissions, groups, notifications, AI agents.
- `scripts/seed/002_seed_domain_reference_data.sql`
  - departments, locations, skills, employees, candidate/application sample data, workflow routing, interview template, AI runtime.
- `scripts/stored-procedures/001_user_procedures.sql`
  - user context, admin user list/detail/update procedures.
- `scripts/stored-procedures/002_workflow_and_candidate_procedures.sql`
  - workflow assignment claim and candidate re-apply checks.

## Tenant Boundary

The schema follows this rule:

```text
Application-owned data = product capability/catalog
Tenant-owned data = one company's configuration, users, and runtime business records
```

Application-owned data:

- `Permissions`
- `AiAgentDefinitions`
- backend constants represented in code, such as workflow action keys and notification event codes
- database schema, migrations, procedures, and views
- the single platform/system role row, `System Administrator`, represented as a role with `TenantId = NULL`

Tenant-owned data:

- `Tenants`, `TenantRecruitmentSettings`, `TenantAiSettings`, `TenantAccessPolicies`
- `AppUsers`, `UserCredentials`, `RefreshTokens`, `UserRoles`
- tenant roles in `Roles` where `TenantId IS NOT NULL`
- tenant-specific `RolePermissions` through those tenant roles
- `Groups`, `GroupMembers`, departments, locations, skills, projects, employees, candidates, Job Requests, job posts/applications, interviews, workflows, notifications, AI logs, embeddings, and audit logs

Important boundary rules:

- Every tenant-owned runtime/configuration table must include and filter by `TenantId`.
- `Permissions` are application-owned; tenants choose how tenant roles map to those permissions.
- `System Administrator` is the only system-wide role. All other roles are tenant-scoped.
- Groups are tenant-owned routing constructs, not permission grants.
- Workflow action keys and notification event codes are code-owned constants. Tenant rows can configure recipients/templates/status, but tenants do not rename or invent backend event/action codes in MVP.

## Schema Domains And Why They Exist

- Tenant configuration
  - `Tenants`, `TenantRecruitmentSettings`, `TenantAiSettings`, `TenantAccessPolicies`
  - Stores tenant identity, status, timezone, currency, career page settings, notification email provider, branding logo, permission policy, and runtime model values.
- Identity and authorization
  - `AppUsers`, `UserCredentials`, `RefreshTokens`, `Roles`, `Permissions`, `RolePermissions`, `UserRoles`
  - Builds auth profile, role display, role priority, and effective permission ids. `Permissions` are application-owned; tenant roles and role mappings are tenant-owned except the platform `System Administrator` role.
- Workflow routing groups
  - `Groups`, `GroupMembers`
  - Routes work to PMO, recruiter, hiring manager, and interviewer groups. Groups do not grant permissions.
- Organization and bench data
  - `Departments`, `Locations`, `Skills`, `Projects`, `Employees`, `EmployeeSkills`, `EmployeeProjectAssignments`
  - Supports PMO bench matching and internal resource recommendations.
- Job request lifecycle
  - `JobRequests`, `JobRequestSkills`, `JobRequestEmployeeReferrals`, `JobRequestFulfillments`
  - Tracks the resource need, required positions, internal referrals, external-candidate joins, and fulfillment.
- Candidate sourcing and application
  - `CandidateSourceLabels`, `Candidates`, `CandidateSkills`, `CandidateProspects`, `CandidateProspectJobRequests`, `CandidateInvitations`, `JobApplications`, `JobApplicationStatusHistory`
  - Keeps sourced prospects, registered candidates, job-specific applications, invite token hashes (`CandidateInvitations.TokenHash`), re-apply history, and hired-candidate-to-employee traceability.
- Hiring pipeline and interviews
  - `InterviewTemplates`, `InterviewTemplateRounds`, `JobRequestInterviewRounds`, `Interviews`, `InterviewFeedback`, `InterviewQuestionBankItems`, `InterviewQuestionRecommendationSets`, `InterviewQuestionRecommendations`
  - Stores fixed job-post pipeline templates, required interview assignments, feedback, audited skipped-interview reasons, seeded interview question-bank RAG items, and versioned AI-generated question recommendation sets.
- Offer and final outcome
  - `OfferLetters`, `OfferPresentationMeetings`
  - Stores editable Hiring Manager offer drafts and physical offer-presentation meetings linked to the candidate application, job post, and Job Request.
- Workflows and baton movement
  - `WorkflowDefinitions`, `WorkflowStages`, `WorkflowTransitions`, `WorkflowRoutingRules`, `JobRequestIntakeRoutingRules`, `WorkflowAssignments`, `WorkflowHistory`
  - Controls operational handoffs such as Presales to PMO, PMO to Recruiter, and Hiring Manager routing. `JobRequestIntakeRoutingRules` is tenant-owned department-to-user/group configuration for Presales-created Job Requests; backend action keys remain code-owned.
- Notifications and audit
  - `NotificationEvents`, `NotificationTemplates`, `NotificationRecipients`, `NotificationOutbox`, `NotificationWorkerStatus`, `AuditLogs`
  - Stores durable notification records, email template text, SignalR/email outbox rows, worker heartbeat status, and audit history.
- AI, vector search, and external tool usage
  - `AiAgentDefinitions`, `AiAgentRuns`, `AiRecommendationLogs`, `VectorEmbeddings`, `ExternalToolDailyUsage`
  - Stores advisory AI execution traces, recommendation explanations, model metadata, source hashes, 768-dimensional embeddings, and durable daily request counts for paid external tools such as Tavily web research. Interview question bank items are embedded as `InterviewQuestionBankItem` vectors for the Interview Question Recommender.

## Persistence Rules

- Every tenant-owned table must include `TenantId`.
- Store all timestamps as UTC `DATETIME2(3)` columns with `Utc` suffixes.
- Return UTC ISO values to frontend; frontend formats local display.
- Tenant timezone is stored as an IANA id, for example `Asia/Karachi`.
- Tenant currency is stored as an ISO 4217 code, for example `PKR`.
- Tenant status is `Active` or `Inactive`.
- Tenant notification email provider is stored on `TenantRecruitmentSettings.NotificationEmailProvider` and is constrained to `Resend` or `MicrosoftGraph`.
- Tenant logo payloads are stored in `TenantRecruitmentSettings` as binary content with file name and content type metadata.
- User list displays highest-priority role from `Roles.Priority`; full assignments remain in `UserRoles`.
- Permission conflict behavior is stored in tenant access policies.
- Bulk role assignment writes role rows plus `RoleAssignmentBatches` for auditability.
- Candidate invite tokens store hashes only, never raw tokens.
- Re-apply checks use final decision timestamp plus configured cooldown.
- Notification delivery is code-owned. Schema stores events, recipients, templates, outbox work, and worker heartbeat diagnostics.
- Vector embeddings must always be tenant-filtered and model/dimension-aware.
- Paid external AI tools must reserve usage in `ExternalToolDailyUsage` before calling providers so daily caps survive API restarts.

## SQL Safety Rules

- Scripts must be idempotent.
- Prefer additive changes.
- Prefer `CREATE OR ALTER PROCEDURE`.
- Do not drop seeded MVP data without explicit approval.
- Keep seed data separate from schema.
- Keep stored procedures separate from seed files.
