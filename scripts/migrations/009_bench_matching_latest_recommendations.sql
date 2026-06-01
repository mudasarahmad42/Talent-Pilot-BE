/*
    Adds latest-run persistence metadata for Bench Matching AI recommendations.
    AiRecommendationLogs keeps the latest recommendation per source Job Request and employee;
    detailed run history remains in AiAgentRuns.
*/

IF COL_LENGTH(N'dbo.AiRecommendationLogs', N'AiAgentRunId') IS NULL
BEGIN
    ALTER TABLE dbo.AiRecommendationLogs ADD AiAgentRunId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.AiRecommendationLogs', N'UpdatedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.AiRecommendationLogs ADD UpdatedAtUtc DATETIME2(3) NULL;
END;
GO

UPDATE dbo.AiRecommendationLogs
SET UpdatedAtUtc = CreatedAtUtc
WHERE UpdatedAtUtc IS NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_AiRecommendationLogs_AgentRuns'
      AND parent_object_id = OBJECT_ID(N'dbo.AiRecommendationLogs')
)
BEGIN
    ALTER TABLE dbo.AiRecommendationLogs
        ADD CONSTRAINT FK_AiRecommendationLogs_AgentRuns
            FOREIGN KEY (AiAgentRunId) REFERENCES dbo.AiAgentRuns (AiAgentRunId);
END;
GO

;WITH ranked AS
(
    SELECT
        AiRecommendationLogId,
        ROW_NUMBER() OVER
        (
            PARTITION BY
                TenantId,
                AiAgentDefinitionId,
                SourceEntityType,
                SourceEntityId,
                RecommendedEntityType,
                RecommendedEntityId
            ORDER BY COALESCE(UpdatedAtUtc, CreatedAtUtc) DESC, CreatedAtUtc DESC
        ) AS RowNumber
    FROM dbo.AiRecommendationLogs
    WHERE AiAgentDefinitionId IS NOT NULL
)
DELETE logs
FROM dbo.AiRecommendationLogs AS logs
INNER JOIN ranked
    ON ranked.AiRecommendationLogId = logs.AiRecommendationLogId
WHERE ranked.RowNumber > 1;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_AiRecommendationLogs_Latest'
      AND object_id = OBJECT_ID(N'dbo.AiRecommendationLogs')
)
BEGIN
    CREATE UNIQUE INDEX UX_AiRecommendationLogs_Latest
        ON dbo.AiRecommendationLogs
        (
            TenantId,
            AiAgentDefinitionId,
            SourceEntityType,
            SourceEntityId,
            RecommendedEntityType,
            RecommendedEntityId
        )
        WHERE AiAgentDefinitionId IS NOT NULL;
END;
GO
