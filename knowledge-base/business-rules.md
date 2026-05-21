# Business Rules

- Job Request is the root lifecycle record.
- Every downstream object should remain linked to JobRequest and BusinessProcessId where applicable.
- Presales-created requests go to PMO review.
- PMO-created requests can skip PMO review and proceed to bench proposal or HR hiring request.
- PMO first checks current employees who are not working on any project and are currently benched.
- PMO can propose one or more bench employees to Presales.
- Internal bench referrals do not need internal interviews in MVP.
- Recruiter/HR gets the request only after PMO requests hiring.
- Recruiter recommendation priority:
  - candidates who cleared similar interviews but were on hold or not offered
  - candidates who cleared some similar interview stages but failed later stages
  - new manual sourcing and Talent Pilot job posting
- Candidate and application are separate.
- Candidate must register/log in before applying.
- Candidate applications use Hiring Pipeline stages, not generic workflow types.
- Recruiter selects a fixed pipeline template per job post.
- Notification delivery is backend-owned.
- SignalR is the realtime delivery path for in-app notifications.
- Email is sent only for important pending work or assignment events.
- AI agents are advisory. They can parse, rank, summarize, and explain, but cannot auto-reject or make final hiring decisions.

## MVP Flow Types

- Resource Request Flow
- Bench Proposal Flow
- Hiring Intake Flow

Flow Types are code-owned and seeded. Tenant Admin can configure templates/decision owners for existing flow types, but should not create arbitrary flow types in MVP.

## MVP AI Agents

- Requirement Parser
- CV Parser
- Bench Matching
- Talent Rediscovery
- Fit Explanation
- Hiring Manager Decision Brief

Model runtime comes from configuration/appsettings and database runtime rows. Do not expose model switching as a normal tenant-admin action during MVP.
