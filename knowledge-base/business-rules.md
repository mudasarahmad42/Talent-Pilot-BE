# Business Rules

Canonical business source of truth: [../../../TALENT_PILOT_SOURCE_OF_TRUTH.md](../../../TALENT_PILOT_SOURCE_OF_TRUTH.md)

- Job Request is the root lifecycle record.
- Every downstream object should remain linked to JobRequest and BusinessProcessId where applicable.
- Presales-created requests go to PMO review through department-based intake routing.
- Department intake routing is tenant configuration: each active department can route new Presales-created requests to one active user or one active group.
- If an active department has no active intake route, the request falls back to the Tenant Admin role so admins can correct the missing configuration.
- PMO-created requests stay in PMO review and are assigned directly to the PMO creator.
- PMO first checks current employees who are not working on any project and are currently benched.
- PMO can recommend one or more bench employees to Presales.
- Presales can accept employees toward fulfillment or reject the recommendation back to PMO.
- Internal bench referrals do not need internal interviews in MVP.
- Recruiter/HR gets the request only after PMO requests hiring.
- Recruiter recommendation priority:
  - candidates who cleared similar interviews but were on hold or not offered
  - candidates who cleared some similar interview stages but failed later stages
  - new manual sourcing and Talent Pilot job posting
- Talent Rediscovery warm-history signals can surface candidates for recruiter review, but zero direct required-skill coverage must remain low-fit and low priority. Historical outcome, interview pass history, and vector similarity must not promote an unrelated candidate above candidates with current required-skill evidence.
- Candidate and application are separate.
- Candidate must register/log in before applying.
- Candidate profile editing belongs to the candidate portal. Email is identity-owned/read-only; profile saves create/update Candidate fields, primary education, current work history, and skills, then best-effort index a `CandidateProfile` vector without blocking the save.
- Candidate applications use Hiring Pipeline stages, not generic workflow types.
- Application document records store provider/container/path/checksum metadata. MVP files use the local Talent Pilot server file-system provider, but this is backend storage metadata so Azure Blob or another provider can replace it later. Candidate-facing UI should not display provider names or storage migration notes.
- Recruiter starts from an interview template and can customize interview rounds per job post.
- Interviewers receive interview tasks tied to candidate application and round; the main Job Request baton does not move to every interviewer.
- After an interviewer submits feedback, the candidate application returns to Recruiter review. Recruiter decides whether to schedule/forward the next round, hold/reject when allowed, or move the application to Hiring Manager Review after all rounds are complete or skipped.
- HOD/department head is an interviewer user/group when used, not an approver.
- Hiring Manager records final outcome and can generate/store an offer draft. Offer approval/signoff is not part of MVP.
- Notification delivery is backend-owned.
- SignalR is the realtime delivery path for in-app notifications.
- Email is sent only for important pending work or assignment events.
- AI agents are advisory. They can parse, rank, summarize, and explain, but cannot auto-reject or make final hiring decisions.
- Permissions are application-owned catalog entries.
- `System Administrator` is the only system-wide role.
- Tenant roles, role-permission mappings, user-role mappings, groups, and group memberships are tenant-owned.
- Groups route work; groups do not grant permissions.
- Workflow action keys and notification event codes are backend-owned constants, not tenant-authored configuration.

## MVP Flow Types

- Resource Request Flow
- Bench Proposal Flow
- Hiring Intake Flow

Flow Types are code-owned and seeded. Tenant Admin can configure templates/decision owners for existing flow types, but should not create arbitrary flow types in MVP.

## MVP AI Agents

- Job Description Drafter: generates editable Job Request description text from controlled intake fields only. Human review is required before save, and saved descriptions are embedded for future semantic agents.
- Requirement Parser
- CV Parser: manual sourcing DOCX parsing prefills editable candidate fields, then stores the parsed CV summary/text as hidden Job Application document evidence and embedding context when the recruiter invites the candidate. Do not surface the parser summary in the form; future cover-letter context should reuse the same application evidence path.
- Candidate Profile Indexing: candidate-owned profile saves generate Candidate/Profile embedding context for rediscovery and ranking. Embedding/vector failures must be swallowed so candidate profile updates still succeed.
- Bench Matching: ranks active internal employees for PMO Review after claim; PMO remains the decision maker.
- Job Post Generator
- Talent Rediscovery: ranks previous warm candidates for claimed Recruiter Sourcing work using candidate history, outcomes, interview feedback, skills, and vectors. It does not use web search, contact candidates, or move workflow stages.
- Fit Explanation
- Feedback Summary
- Hiring Manager Decision Brief

Model runtime comes from configuration/appsettings and database runtime rows. Do not expose model switching as a normal tenant-admin action during MVP.
