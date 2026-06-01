/*
    Adds the Job Description Drafting Agent to existing databases.
    The agent is code-owned and produces editable Job Request description text from structured intake fields.
*/

MERGE dbo.AiAgentDefinitions AS target
USING (VALUES
    (
        N'job-description-drafter',
        N'Job Description Drafter',
        N'Drafts editable Job Request descriptions from controlled intake fields.',
        N'Job title, client, department, location, selected tenant skills, experience range, required positions, priority, and hiring manager.',
        N'Plain-text job description ready for human editing.',
        N'Human review is required before save; the agent cannot approve, reject, or move workflow stages.',
        CAST(1 AS BIT)
    )
) AS source (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled)
ON target.AiAgentDefinitionId = source.AiAgentDefinitionId
WHEN MATCHED THEN
    UPDATE SET
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
