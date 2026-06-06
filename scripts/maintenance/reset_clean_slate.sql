/*
    Reset the local TalentPilot database to a clean workflow slate.

    Cleared:
      - Job requests, job posts, applications, interviews, offers, workflow tasks/history.
      - Candidate invitations/prospects, candidate profile details/documents, RAG index/conversations.
      - AI agent runs/recommendations, notification outbox/recipients/worker status, audit logs.
      - External tool usage counters, temporary OAuth states, demo project allocations/projects.

    Preserved:
      - Tenants, tenant recruitment/AI settings, users, credentials, roles, groups, permissions.
      - Departments, locations, skills, candidate source labels, employees and employee skills.
      - Workflow definitions/configuration, notification events/templates, AI agent definitions.
      - Interview templates and question-bank items.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Targets TABLE
    (
        DeleteOrder INT NOT NULL PRIMARY KEY,
        TableName SYSNAME NOT NULL UNIQUE
    );

    INSERT INTO @Targets (DeleteOrder, TableName)
    VALUES
        ( 10, N'AiAssistantFeedback'),
        ( 20, N'AiAssistantMessageCitations'),
        ( 30, N'AiAssistantMessages'),
        ( 40, N'AiAssistantConversations'),
        ( 50, N'InterviewQuestionRecommendations'),
        ( 60, N'InterviewQuestionRecommendationSets'),
        ( 70, N'AiRecommendationLogs'),
        ( 80, N'OnlineCandidateLeads'),
        ( 90, N'OnlineCandidateSourcingRuns'),
        (100, N'JobApplicationDocuments'),
        (110, N'CandidateProfileDocuments'),
        (120, N'CandidateSkills'),
        (130, N'CandidateEducation'),
        (140, N'CandidateWorkHistory'),
        (150, N'OfferPresentationMeetings'),
        (160, N'OfferLetters'),
        (170, N'InterviewFeedback'),
        (180, N'InterviewParticipants'),
        (190, N'Interviews'),
        (200, N'JobRequestFulfillments'),
        (210, N'JobApplicationStatusHistory'),
        (220, N'JobRequestEmployeeReferrals'),
        (230, N'JobApplications'),
        (240, N'CandidateInvitations'),
        (250, N'CandidateProspectJobRequests'),
        (260, N'CandidateProspects'),
        (270, N'JobPostInterviewRounds'),
        (280, N'JobPostSkills'),
        (290, N'JobPosts'),
        (300, N'JobRequestInterviewRounds'),
        (310, N'JobRequestSkills'),
        (320, N'WorkflowHistory'),
        (330, N'WorkflowAssignments'),
        (340, N'JobRequests'),
        (350, N'NotificationRecipients'),
        (360, N'NotificationOutbox'),
        (370, N'NotificationWorkerStatus'),
        (380, N'AuditLogs'),
        (390, N'AiAgentRuns'),
        (400, N'KnowledgeChunks'),
        (410, N'VectorEmbeddings'),
        (420, N'ExternalToolDailyUsage'),
        (430, N'RoleAssignmentBatches'),
        (440, N'GoogleCalendarOAuthStates'),
        (450, N'EmployeeProjectAssignments'),
        (460, N'Projects');

    DECLARE @SchemaName SYSNAME;
    DECLARE @TableName SYSNAME;
    DECLARE @Sql NVARCHAR(MAX);
    DECLARE @DeletedRows INT;

    DECLARE disable_constraints CURSOR LOCAL FAST_FORWARD FOR
        SELECT SCHEMA_NAME(schema_id), name
        FROM sys.tables
        WHERE is_ms_shipped = 0
        ORDER BY SCHEMA_NAME(schema_id), name;

    OPEN disable_constraints;
    FETCH NEXT FROM disable_constraints INTO @SchemaName, @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + N' NOCHECK CONSTRAINT ALL;';
        EXEC sys.sp_executesql @Sql;

        FETCH NEXT FROM disable_constraints INTO @SchemaName, @TableName;
    END;

    CLOSE disable_constraints;
    DEALLOCATE disable_constraints;

    DECLARE delete_targets CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName
        FROM @Targets
        WHERE OBJECT_ID(N'dbo.' + TableName, N'U') IS NOT NULL
        ORDER BY DeleteOrder;

    OPEN delete_targets;
    FETCH NEXT FROM delete_targets INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DeletedRows = 0;
        SET @Sql = N'DELETE FROM dbo.' + QUOTENAME(@TableName) + N'; SET @Rows = @@ROWCOUNT;';

        EXEC sys.sp_executesql
            @Sql,
            N'@Rows INT OUTPUT',
            @Rows = @DeletedRows OUTPUT;

        FETCH NEXT FROM delete_targets INTO @TableName;
    END;

    CLOSE delete_targets;
    DEALLOCATE delete_targets;

    IF OBJECT_ID(N'dbo.Candidates', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.Candidates
        SET
            Phone = NULL,
            LinkedInUrl = NULL,
            CurrentDesignation = NULL,
            CurrentCompany = NULL,
            ExperienceYears = NULL,
            ExpectedSalaryAmount = NULL,
            ExpectedSalaryCurrency = NULL,
            NoticePeriodDays = NULL,
            Status = N'Active',
            UpdatedAtUtc = SYSUTCDATETIME();
    END;

    IF OBJECT_ID(N'dbo.Employees', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.Employees
        SET
            AvailabilityStatus = N'Available',
            BenchStatus = N'Benched',
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE Status = N'Active';
    END;

    DECLARE enable_constraints CURSOR LOCAL FAST_FORWARD FOR
        SELECT SCHEMA_NAME(schema_id), name
        FROM sys.tables
        WHERE is_ms_shipped = 0
        ORDER BY SCHEMA_NAME(schema_id), name;

    OPEN enable_constraints;
    FETCH NEXT FROM enable_constraints INTO @SchemaName, @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + N' WITH CHECK CHECK CONSTRAINT ALL;';
        EXEC sys.sp_executesql @Sql;

        FETCH NEXT FROM enable_constraints INTO @SchemaName, @TableName;
    END;

    CLOSE enable_constraints;
    DEALLOCATE enable_constraints;

    DECLARE @Failures NVARCHAR(MAX) = N'';
    DECLARE @RemainingRows BIGINT;

    DECLARE verify_empty CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName
        FROM @Targets
        WHERE OBJECT_ID(N'dbo.' + TableName, N'U') IS NOT NULL
        ORDER BY DeleteOrder;

    OPEN verify_empty;
    FETCH NEXT FROM verify_empty INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @RemainingRows = 0;
        SET @Sql = N'SELECT @Rows = COUNT_BIG(1) FROM dbo.' + QUOTENAME(@TableName) + N';';

        EXEC sys.sp_executesql
            @Sql,
            N'@Rows BIGINT OUTPUT',
            @Rows = @RemainingRows OUTPUT;

        IF @RemainingRows > 0
        BEGIN
            SET @Failures = CONCAT(@Failures, @TableName, N'=', @RemainingRows, N'; ');
        END;

        FETCH NEXT FROM verify_empty INTO @TableName;
    END;

    CLOSE verify_empty;
    DEALLOCATE verify_empty;

    IF LEN(@Failures) > 0
    BEGIN
        DECLARE @FailureMessage NVARCHAR(2048) = CONCAT(N'Clean-slate reset failed. Rows remained in cleared tables: ', LEFT(@Failures, 1900));
        THROW 51000, @FailureMessage, 1;
    END;

    IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.AppUsers)
        THROW 51001, N'Clean-slate reset verification failed: AppUsers were not preserved.', 1;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Roles)
        THROW 51002, N'Clean-slate reset verification failed: Roles were not preserved.', 1;

    IF OBJECT_ID(N'dbo.Skills', N'U') IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Skills)
        THROW 51003, N'Clean-slate reset verification failed: Skills were not preserved.', 1;

    IF OBJECT_ID(N'dbo.AiAgentDefinitions', N'U') IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.AiAgentDefinitions)
        THROW 51004, N'Clean-slate reset verification failed: AI agent definitions were not preserved.', 1;

    IF OBJECT_ID(N'dbo.InterviewQuestionBankItems', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.InterviewQuestionBankItems)
        THROW 51005, N'Clean-slate reset verification failed: interview question bank is empty.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
