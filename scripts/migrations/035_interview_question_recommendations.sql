IF OBJECT_ID(N'dbo.InterviewQuestionBankItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewQuestionBankItems
    (
        InterviewQuestionBankItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewQuestionBankItems PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        JobFamily NVARCHAR(160) NULL,
        RoundType NVARCHAR(40) NOT NULL,
        Difficulty NVARCHAR(40) NOT NULL,
        QuestionText NVARCHAR(1000) NOT NULL,
        ExpectedSignal NVARCHAR(1000) NOT NULL,
        FollowUpsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_InterviewQuestionBankItems_FollowUpsJson DEFAULT N'[]',
        EvaluationRubricJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_InterviewQuestionBankItems_EvaluationRubricJson DEFAULT N'[]',
        SourceTitle NVARCHAR(240) NULL,
        SourceUrl NVARCHAR(500) NULL,
        ContentHashSha256 NVARCHAR(128) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_InterviewQuestionBankItems_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewQuestionBankItems_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewQuestionBankItems_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InterviewQuestionBankItems_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewQuestionBankItems_Skills FOREIGN KEY (SkillId) REFERENCES dbo.Skills (SkillId),
        CONSTRAINT FK_InterviewQuestionBankItems_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
        CONSTRAINT CK_InterviewQuestionBankItems_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT CK_InterviewQuestionBankItems_FollowUpsJson CHECK (ISJSON(FollowUpsJson) = 1),
        CONSTRAINT CK_InterviewQuestionBankItems_EvaluationRubricJson CHECK (ISJSON(EvaluationRubricJson) = 1),
        CONSTRAINT UQ_InterviewQuestionBankItems_Tenant_Hash UNIQUE (TenantId, ContentHashSha256)
    );
END;
GO

IF OBJECT_ID(N'dbo.InterviewQuestionRecommendationSets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewQuestionRecommendationSets
    (
        RecommendationSetId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewQuestionRecommendationSets PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InterviewId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        JobPostInterviewRoundId UNIQUEIDENTIFIER NOT NULL,
        AiAgentRunId UNIQUEIDENTIFIER NOT NULL,
        ModelName NVARCHAR(100) NOT NULL,
        PromptVersion NVARCHAR(100) NOT NULL,
        VersionNumber INT NOT NULL,
        Summary NVARCHAR(MAX) NOT NULL,
        Rationale NVARCHAR(MAX) NULL,
        RegenerateReason NVARCHAR(500) NULL,
        CoverageJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendationSets_CoverageJson DEFAULT N'{}',
        RetrievedBankItemIdsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendationSets_RetrievedBankItemIdsJson DEFAULT N'[]',
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendationSets_Status DEFAULT N'Active',
        GeneratedByUserId UNIQUEIDENTIFIER NOT NULL,
        GeneratedAtUtc DATETIME2(3) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendationSets_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendationSets_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InterviewQuestionRecommendationSets_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewQuestionRecommendationSets_Interviews FOREIGN KEY (InterviewId) REFERENCES dbo.Interviews (InterviewId),
        CONSTRAINT FK_InterviewQuestionRecommendationSets_JobApplications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_InterviewQuestionRecommendationSets_Rounds FOREIGN KEY (JobPostInterviewRoundId) REFERENCES dbo.JobPostInterviewRounds (JobPostInterviewRoundId),
        CONSTRAINT FK_InterviewQuestionRecommendationSets_AiRuns FOREIGN KEY (AiAgentRunId) REFERENCES dbo.AiAgentRuns (AiAgentRunId),
        CONSTRAINT FK_InterviewQuestionRecommendationSets_GeneratedBy FOREIGN KEY (GeneratedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_InterviewQuestionRecommendationSets_Status CHECK (Status IN (N'Active', N'Archived')),
        CONSTRAINT CK_InterviewQuestionRecommendationSets_Version CHECK (VersionNumber > 0),
        CONSTRAINT CK_InterviewQuestionRecommendationSets_CoverageJson CHECK (ISJSON(CoverageJson) = 1),
        CONSTRAINT CK_InterviewQuestionRecommendationSets_RetrievedBankItemIdsJson CHECK (ISJSON(RetrievedBankItemIdsJson) = 1),
        CONSTRAINT UQ_InterviewQuestionRecommendationSets_Interview_Version UNIQUE (TenantId, InterviewId, VersionNumber)
    );
END;
GO

IF OBJECT_ID(N'dbo.InterviewQuestionRecommendations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewQuestionRecommendations
    (
        QuestionRecommendationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewQuestionRecommendations PRIMARY KEY,
        RecommendationSetId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SortOrder INT NOT NULL,
        QuestionText NVARCHAR(1200) NOT NULL,
        QuestionType NVARCHAR(60) NOT NULL,
        RoundType NVARCHAR(60) NOT NULL,
        SkillName NVARCHAR(160) NULL,
        Difficulty NVARCHAR(40) NOT NULL,
        Rationale NVARCHAR(1000) NOT NULL,
        ExpectedSignal NVARCHAR(1000) NOT NULL,
        FollowUpsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendations_FollowUpsJson DEFAULT N'[]',
        EvaluationRubricJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendations_EvaluationRubricJson DEFAULT N'[]',
        SourceBankItemId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewQuestionRecommendations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InterviewQuestionRecommendations_Sets FOREIGN KEY (RecommendationSetId) REFERENCES dbo.InterviewQuestionRecommendationSets (RecommendationSetId) ON DELETE CASCADE,
        CONSTRAINT FK_InterviewQuestionRecommendations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewQuestionRecommendations_SourceBankItem FOREIGN KEY (SourceBankItemId) REFERENCES dbo.InterviewQuestionBankItems (InterviewQuestionBankItemId),
        CONSTRAINT CK_InterviewQuestionRecommendations_SortOrder CHECK (SortOrder > 0),
        CONSTRAINT CK_InterviewQuestionRecommendations_FollowUpsJson CHECK (ISJSON(FollowUpsJson) = 1),
        CONSTRAINT CK_InterviewQuestionRecommendations_EvaluationRubricJson CHECK (ISJSON(EvaluationRubricJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InterviewQuestionBankItems_Tenant_Round_Skill' AND object_id = OBJECT_ID(N'dbo.InterviewQuestionBankItems'))
    CREATE INDEX IX_InterviewQuestionBankItems_Tenant_Round_Skill ON dbo.InterviewQuestionBankItems (TenantId, Status, RoundType, SkillId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InterviewQuestionBankItems_Tenant_Family' AND object_id = OBJECT_ID(N'dbo.InterviewQuestionBankItems'))
    CREATE INDEX IX_InterviewQuestionBankItems_Tenant_Family ON dbo.InterviewQuestionBankItems (TenantId, Status, JobFamily, RoundType);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InterviewQuestionRecommendationSets_Latest' AND object_id = OBJECT_ID(N'dbo.InterviewQuestionRecommendationSets'))
    CREATE INDEX IX_InterviewQuestionRecommendationSets_Latest ON dbo.InterviewQuestionRecommendationSets (TenantId, InterviewId, Status, VersionNumber DESC, GeneratedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InterviewQuestionRecommendations_Set_Order' AND object_id = OBJECT_ID(N'dbo.InterviewQuestionRecommendations'))
    CREATE INDEX IX_InterviewQuestionRecommendations_Set_Order ON dbo.InterviewQuestionRecommendations (TenantId, RecommendationSetId, SortOrder);
GO

MERGE dbo.AiAgentDefinitions AS target
USING (VALUES
    (N'interview-question-recommender', N'Interview Question Recommender', N'Recommends interviewer-facing questions for HR, screening, technical, HOD, and behavioral rounds using interview context, seeded question-bank retrieval, vector ranking, and LLM-generated structured output.', N'Assigned interview task, job request/post details, required skills, candidate profile, cover letter, application document excerpts, prior interview evidence, and retrieved question-bank items.', N'Natural-language summary plus structured question objects with rationale, expected signal, follow-ups, rubric, source bank item, coverage, model, prompt version, and run metadata.', N'Advisory only. Interviewers own final assessment; the agent cannot submit feedback, hire, reject, schedule, contact candidates, or move workflow stages.', CAST(1 AS BIT))
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
WHEN NOT MATCHED THEN INSERT
    (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (source.AiAgentDefinitionId, source.DisplayName, source.Responsibility, source.InputSummary, source.OutputSummary, source.MvpBoundary, source.Enabled, SYSUTCDATETIME(), SYSUTCDATETIME());
GO
