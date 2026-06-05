/*
    Adds lead-only online candidate sourcing persistence for the Online Headhunting Agent.
    These rows are not Candidates or JobApplications until a recruiter explicitly converts them.
*/

IF OBJECT_ID(N'dbo.OnlineCandidateSourcingRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineCandidateSourcingRuns
    (
        OnlineCandidateSourcingRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OnlineCandidateSourcingRuns PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        JobPostId UNIQUEIDENTIFIER NULL,
        RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
        AiAgentRunId UNIQUEIDENTIFIER NULL,
        SearchMoreFromRunId UNIQUEIDENTIFIER NULL,
        RequestedLimit INT NOT NULL,
        DailyLeadLimit INT NOT NULL,
        DailyLeadCountBeforeRun INT NOT NULL,
        SourceCodesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OnlineCandidateSourcingRuns_SourceCodesJson DEFAULT N'[]',
        QueriesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OnlineCandidateSourcingRuns_QueriesJson DEFAULT N'[]',
        SearchStatus NVARCHAR(200) NOT NULL,
        Model NVARCHAR(160) NOT NULL,
        LeadsReturned INT NOT NULL CONSTRAINT DF_OnlineCandidateSourcingRuns_LeadsReturned DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_OnlineCandidateSourcingRuns_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OnlineCandidateSourcingRuns_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_OnlineCandidateSourcingRuns_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_OnlineCandidateSourcingRuns_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId),
        CONSTRAINT FK_OnlineCandidateSourcingRuns_RequestedByUser FOREIGN KEY (RequestedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_OnlineCandidateSourcingRuns_AiAgentRuns FOREIGN KEY (AiAgentRunId) REFERENCES dbo.AiAgentRuns (AiAgentRunId),
        CONSTRAINT FK_OnlineCandidateSourcingRuns_SearchMore FOREIGN KEY (SearchMoreFromRunId) REFERENCES dbo.OnlineCandidateSourcingRuns (OnlineCandidateSourcingRunId),
        CONSTRAINT CK_OnlineCandidateSourcingRuns_Limits CHECK (RequestedLimit BETWEEN 1 AND 20 AND DailyLeadLimit BETWEEN 1 AND 100 AND DailyLeadCountBeforeRun >= 0 AND LeadsReturned >= 0),
        CONSTRAINT CK_OnlineCandidateSourcingRuns_SourceCodesJson CHECK (ISJSON(SourceCodesJson) = 1),
        CONSTRAINT CK_OnlineCandidateSourcingRuns_QueriesJson CHECK (ISJSON(QueriesJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.OnlineCandidateLeads', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineCandidateLeads
    (
        OnlineCandidateLeadId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OnlineCandidateLeads PRIMARY KEY,
        OnlineCandidateSourcingRunId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        Rank INT NOT NULL,
        SourceCode NVARCHAR(60) NOT NULL,
        SourceDisplayName NVARCHAR(120) NOT NULL,
        SourceUrl NVARCHAR(1000) NOT NULL,
        DisplayName NVARCHAR(200) NULL,
        CurrentTitle NVARCHAR(200) NULL,
        CurrentCompany NVARCHAR(200) NULL,
        LocationText NVARCHAR(200) NULL,
        Email NVARCHAR(320) NULL,
        Phone NVARCHAR(50) NULL,
        ProfileUrl NVARCHAR(1000) NULL,
        EvidenceSnippet NVARCHAR(1200) NOT NULL,
        MatchScore DECIMAL(5,2) NOT NULL,
        Confidence NVARCHAR(30) NOT NULL,
        FitSummary NVARCHAR(1200) NOT NULL,
        StrengthsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OnlineCandidateLeads_StrengthsJson DEFAULT N'[]',
        MatchedSkillsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OnlineCandidateLeads_MatchedSkillsJson DEFAULT N'[]',
        GapsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OnlineCandidateLeads_GapsJson DEFAULT N'[]',
        MissingDataJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OnlineCandidateLeads_MissingDataJson DEFAULT N'[]',
        DuplicateStatus NVARCHAR(40) NOT NULL,
        DuplicateCandidateId UNIQUEIDENTIFIER NULL,
        DuplicateCandidateName NVARCHAR(200) NULL,
        DuplicateExplanation NVARCHAR(600) NULL,
        OutreachDraft NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_OnlineCandidateLeads_Status DEFAULT N'New',
        ConvertedCandidateId UNIQUEIDENTIFIER NULL,
        ConvertedJobApplicationId UNIQUEIDENTIFIER NULL,
        ConvertedAtUtc DATETIME2(3) NULL,
        RejectedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_OnlineCandidateLeads_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OnlineCandidateLeads_Runs FOREIGN KEY (OnlineCandidateSourcingRunId) REFERENCES dbo.OnlineCandidateSourcingRuns (OnlineCandidateSourcingRunId),
        CONSTRAINT FK_OnlineCandidateLeads_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_OnlineCandidateLeads_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_OnlineCandidateLeads_DuplicateCandidate FOREIGN KEY (DuplicateCandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_OnlineCandidateLeads_ConvertedCandidate FOREIGN KEY (ConvertedCandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_OnlineCandidateLeads_ConvertedApplication FOREIGN KEY (ConvertedJobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT CK_OnlineCandidateLeads_Rank CHECK (Rank > 0),
        CONSTRAINT CK_OnlineCandidateLeads_MatchScore CHECK (MatchScore BETWEEN 0 AND 100),
        CONSTRAINT CK_OnlineCandidateLeads_Status CHECK (Status IN (N'New', N'Shortlisted', N'Rejected', N'Converted')),
        CONSTRAINT CK_OnlineCandidateLeads_DuplicateStatus CHECK (DuplicateStatus IN (N'ExactMatch', N'PossibleDuplicate', N'NoMatch')),
        CONSTRAINT CK_OnlineCandidateLeads_StrengthsJson CHECK (ISJSON(StrengthsJson) = 1),
        CONSTRAINT CK_OnlineCandidateLeads_MatchedSkillsJson CHECK (ISJSON(MatchedSkillsJson) = 1),
        CONSTRAINT CK_OnlineCandidateLeads_GapsJson CHECK (ISJSON(GapsJson) = 1),
        CONSTRAINT CK_OnlineCandidateLeads_MissingDataJson CHECK (ISJSON(MissingDataJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnlineCandidateSourcingRuns_JobRequest' AND object_id = OBJECT_ID(N'dbo.OnlineCandidateSourcingRuns'))
    CREATE INDEX IX_OnlineCandidateSourcingRuns_JobRequest ON dbo.OnlineCandidateSourcingRuns (TenantId, JobRequestId, CreatedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnlineCandidateLeads_JobRequest_Created' AND object_id = OBJECT_ID(N'dbo.OnlineCandidateLeads'))
    CREATE INDEX IX_OnlineCandidateLeads_JobRequest_Created ON dbo.OnlineCandidateLeads (TenantId, JobRequestId, CreatedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnlineCandidateLeads_Run_Rank' AND object_id = OBJECT_ID(N'dbo.OnlineCandidateLeads'))
    CREATE INDEX IX_OnlineCandidateLeads_Run_Rank ON dbo.OnlineCandidateLeads (TenantId, OnlineCandidateSourcingRunId, Rank);
GO

MERGE dbo.AiAgentDefinitions AS target
USING (VALUES
    (N'online-headhunting', N'Online Headhunting', N'Discovers lead-only online candidate results from approved search sources, summarizes fit, and checks likely internal duplicates before recruiter review.', N'Claimed Recruiter Sourcing job request/post, required skills, location, experience range, source filters, existing candidate pool identifiers, web-search snippets, and optional GitHub public profile metadata.', N'Lead-only online sourcing run with source links, match score, fit summary, gaps, missing data, duplicate status, outreach draft, source status, model, and run metadata.', N'Advisory and lead-only. It cannot scrape LinkedIn or Indeed, cannot message candidates on external platforms, cannot create candidates/applications, and cannot move workflow stages.', CAST(1 AS BIT))
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
