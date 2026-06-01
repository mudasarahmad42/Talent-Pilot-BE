-- 019_update_ai_agent_runtime_contracts.sql
-- Updates AI agent catalog copy so Admin Center reflects the current runtime flows.

SET NOCOUNT ON;

UPDATE dbo.AiAgentDefinitions
SET
    Responsibility = N'Builds the saved requirement profile used for future semantic matching when a Job Request is created.',
    InputSummary = N'Controlled Job Request intake fields, final saved description, department, location, skills, experience range, positions, and priority.',
    OutputSummary = N'Indexed requirement profile and embedding metadata for downstream agents.',
    MvpBoundary = N'Runs after save; it cannot approve, reject, or move workflow stages.',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AiAgentDefinitionId = N'requirement-parser';

UPDATE dbo.AiAgentDefinitions
SET
    Responsibility = N'Prefills recruiter manual sourcing forms from DOCX resumes.',
    InputSummary = N'DOCX text extracted server-side from the Add Candidate flow.',
    OutputSummary = N'Structured candidate contact, profile, education, experience, and skill evidence for recruiter review.',
    MvpBoundary = N'DOCX only for MVP; recruiters review and edit every extracted field before inviting.',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AiAgentDefinitionId = N'cv-parser';

UPDATE dbo.AiAgentDefinitions
SET
    Responsibility = N'Explains why an employee or candidate was ranked in Bench Matching or Talent Rediscovery.',
    InputSummary = N'Recommendation evidence, skills, experience, location, project/application history, interview evidence, and gaps.',
    OutputSummary = N'Readable strengths, gaps, confidence notes, and caveats embedded in the ranking result.',
    MvpBoundary = N'Explanation supports human review only and never selects or contacts candidates by itself.',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AiAgentDefinitionId = N'fit-explanation';

UPDATE dbo.AiAgentDefinitions
SET
    Responsibility = N'Summarizes interview feedback and candidate context on Hiring Manager Review.',
    InputSummary = N'Candidate profile, source details, recruiter notes, job request/post summary, interview statuses, scores, recommendations, and skipped-round reasons.',
    OutputSummary = N'Advisory decision brief shown to the Hiring Manager before offer or final outcome actions.',
    MvpBoundary = N'Hiring Manager owns the final decision; the brief cannot generate offers or close requests by itself.',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AiAgentDefinitionId = N'hiring-manager-decision-brief';
