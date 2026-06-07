# Talent Pilot SQL Script Conventions

`TalentPilot.Database` runs plain SQL files in this order:

1. `scripts/schema/*.sql`
2. `scripts/migrations/*.sql`
3. `scripts/seed/*.sql`
4. `scripts/stored-procedures/*.sql`

Files are sorted by name inside each folder. Use numeric prefixes such as `001_`, `002_`, and keep every script re-runnable.

## Current MVP Scripts

- `schema/001_create_tables.sql` creates tenant, access control, notification, audit, AI agent, and vector tables.
- `migrations/001_align_workflow_routing_source_of_truth.sql` updates existing developer databases to the current Talent Pilot workflow routing model.
- `migrations/002_add_default_interviewers_to_pipeline_rounds.sql` adds user-level default interviewer assignment support.
- `migrations/003_add_employee_joining_date_to_employees.sql` adds employee joining dates for interviewer picker tenure display.
- `migrations/004_required_interview_rounds_and_skipped_interviews.sql` corrects all interview template rounds to required and adds audited skipped interview support.
- `migrations/005_remove_workflow_action_permissions.sql` drops retired tenant-configurable workflow action authorization rows; backend code owns these rules.
- `migrations/006_seed_job_description_drafter_agent.sql` registers the code-owned Job Description Drafting Agent for existing databases.
- `migrations/007_update_default_llm_model_to_llama32.sql` updates existing tenant AI settings to the locally installed `llama3.2` model.
- `migrations/008_pmo_review_presales_referral_flow.sql` adds the PMO manual employee recommendation and Presales review workflow stage, transitions, and notifications.
- `migrations/009_bench_matching_latest_recommendations.sql` adds latest-run persistence metadata and uniqueness for Bench Matching AI recommendation logs.
- `migrations/010_seed_bench_employee_project_evidence.sql` adds demo project/client evidence for benched employees used by Bench Matching explanations.
- `migrations/011_add_external_tool_daily_usage.sql` adds durable daily request counting for paid external AI tools such as Tavily web research.
- `migrations/012_add_recruiter_job_posts.sql` adds recruiter-owned Job Posts, Job Post skills/rounds, and forward-compatible Job Application linkage.
- `migrations/013_grant_pmo_create_job_request_permission.sql` aligns PMO permissions with the rule that PMO-created Job Requests stay assigned to the PMO creator for PMO Review.
- `migrations/014_seed_talent_rediscovery_demo_candidates.sql` updates the Talent Rediscovery agent catalog entry and adds guarded demo historical candidates/applications/interview feedback for existing developer databases.
- `migrations/015_job_portal_applications_manual_sourcing.sql` adds candidate portal/manual sourcing metadata, education/work-history tables, and the Job Portal source label for existing databases.
- `migrations/016_add_job_post_rounds_to_interviews.sql` links scheduled interviews to job-post runtime rounds for recruiter-owned candidate scheduling.
- `migrations/017_candidate_apply_interview_scheduling_feedback.sql` adds candidate apply/interview scheduling and feedback flow support.
- `migrations/018_hiring_manager_offer_outcome.sql` adds Hiring Manager review, offer letter, presentation meeting, and final outcome support.
- `migrations/019_update_ai_agent_runtime_contracts.sql` updates AI agent catalog copy for the current seven runtime flows.
- `migrations/020_remove_redundant_ai_and_candidate_sync_artifacts.sql` removes retired automatic-stage-movement AI settings plus unused candidate-to-employee sync and candidate document tables.
- `migrations/021_add_hod_role_and_demo_user.sql` adds the tenant-scoped HOD / Department Head role, Engineering HOD demo user, and final-round HOD defaults for existing databases.
- `migrations/022_harden_demo_authentication.sql` updates demo user UPNs and seeds BCrypt password hashes so demo cards use the normal login endpoint.
- `migrations/023_add_job_application_documents.sql` adds application document metadata backed by a swappable storage provider. MVP stores files on the API server filesystem.
- `migrations/024_add_applicant_ranking_cover_letter.sql` adds cover-letter support to current job applications and registers the Applicant Ranking AI agent.
- `migrations/025_fix_offer_declined_seed_interviews.sql` repairs demo Talent Rediscovery data so `OfferDeclined` evidence only appears after all configured interviews are cleared.
- `migrations/026_application_document_extraction_evidence.sql` adds persisted document text extraction metadata for application CV/cover-letter evidence and demo extracted text backfill.
- `migrations/027_interview_google_calendar_metadata.sql` adds Google Calendar event metadata columns to scheduled interviews.
- `migrations/028_notification_worker_status.sql` adds durable notification worker heartbeat status for Admin Center outbox diagnostics.
- `migrations/029_add_notification_email_provider_setting.sql` adds the tenant-level notification email provider setting for Resend or Microsoft Graph delivery.
- `migrations/030_google_calendar_oauth_connections.sql` adds tables for future organizer-scoped Google Calendar OAuth connections.
- `migrations/031_normalize_demo_seed_email_domains.sql` replaces old demo `@talentpilot.test` seed email addresses with the Microsoft tenant test domain.
- `migrations/032_add_interview_participants.sql` stores meeting attendees for scheduled interview events and backfills existing interviews.
- `migrations/035_interview_question_recommendations.sql` adds the Interview Question Recommender agent, question-bank RAG tables, and versioned interview question recommendation persistence.
- `migrations/036_online_headhunting_agent.sql` adds Online Headhunting agent registration plus lead-only `OnlineCandidateSourcingRuns` and `OnlineCandidateLeads` persistence.
- `migrations/041_add_candidate_profile_documents.sql` adds candidate profile resume/CV document metadata used as the fallback for applications without an application-specific CV.
- `migrations/044_seed_react_talent_rediscovery_candidates.sql` seeds 45 warm historical React/frontend candidates with skills, prior applications, interviews, feedback, and extracted CV text for Talent Rediscovery demos after database cleanup.
- `seed/001_seed_initial_data.sql` seeds the TKXEL demo tenant, system roles, permissions, routing groups, notification events/templates, AI agent definitions, and demo users.
- `stored-procedures/001_user_procedures.sql` creates simple auth/admin user helper procedures.

## Migration Rules

- Use UTC timestamp columns with a `Utc` suffix, normally `DATETIME2(3)` with `SYSUTCDATETIME()`.
- Put `TenantId` on tenant-owned tables and filter by it in procedures.
- Keep groups for workflow routing only. Permissions come from roles.
- Prefer `CREATE OR ALTER PROCEDURE` for stored procedures.
- For additive schema changes, create a new numbered script. Do not rewrite historical scripts after teammates have run them.
- For data corrections that existing databases need, create a numbered migration instead of relying only on edited seed files.
- For destructive changes, document the data impact in the script header before making the change.
