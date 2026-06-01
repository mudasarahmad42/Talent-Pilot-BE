IF COL_LENGTH(N'dbo.JobApplications', N'CoverLetterText') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications
    ADD CoverLetterText NVARCHAR(MAX) NULL;
END;
GO

MERGE dbo.AiAgentDefinitions AS target
USING
(
    VALUES
    (
        N'applicant-ranking',
        N'Applicant Ranking',
        N'Ranks current applications for an active job post using candidate profile data, application evidence, uploaded CV/cover-letter context, interview history, and vector similarity. No web search is used.',
        N'Claimed Recruiter Sourcing job post, current active job-post applications, candidate skills/profile fields, cover letter, uploaded application documents, historical applications, interview feedback, and application/job post embeddings.',
        N'Ranked current applications with deterministic score, confidence, matched skills, gaps, document evidence, historical outcome evidence, semantic similarity status, and recruiter-facing rationale.',
        N'Recruiters decide whether to shortlist, schedule, hold, reject, or forward. The agent cannot contact candidates or move workflow stages.',
        CAST(1 AS BIT)
    )
) AS source (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled)
ON target.AiAgentDefinitionId = source.AiAgentDefinitionId
WHEN MATCHED THEN UPDATE SET
    DisplayName = source.DisplayName,
    Responsibility = source.Responsibility,
    InputSummary = source.InputSummary,
    OutputSummary = source.OutputSummary,
    MvpBoundary = source.MvpBoundary,
    Enabled = source.Enabled,
    UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.AiAgentDefinitionId, source.DisplayName, source.Responsibility, source.InputSummary, source.OutputSummary, source.MvpBoundary, source.Enabled, SYSUTCDATETIME(), SYSUTCDATETIME());
GO
