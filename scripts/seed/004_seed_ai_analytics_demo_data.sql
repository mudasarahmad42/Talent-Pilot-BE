-- 004_seed_ai_analytics_demo_data.sql
-- Expands demo data for analytics, AI-agent verification, and end-to-end workflow testing.

SET NOCOUNT ON;

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @WorkflowDefinitionId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000001';
DECLARE @StagePmoReviewId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000012';
DECLARE @StageSourcingId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000013';
DECLARE @StageInterviewingId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000014';
DECLARE @StageHiringManagerId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000015';
DECLARE @StageClosedId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000017';
DECLARE @EngineeringDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';
DECLARE @DevOpsDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03';
DECLARE @LahoreLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02';
DECLARE @RemoteLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03';
DECLARE @PresalesUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333302';
DECLARE @PmoUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333303';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @HodUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333311';
DECLARE @TenantAdminUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333301';
DECLARE @CandidateRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222208';
DECLARE @PmoEngineeringGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444401';
DECLARE @RecruitingGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444402';
DECLARE @ScreeningTemplateRoundId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff11';
DECLARE @TechnicalTemplateRoundId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff12';
DECLARE @HodTemplateRoundId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff13';
DECLARE @SourceLinkedInId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc001';
DECLARE @SourceIndeedId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc002';
DECLARE @SourceReferralId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc003';
DECLARE @SourceOtherId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc004';
DECLARE @SourceJobPortalId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc005';
DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @DemoPasswordHash NVARCHAR(500) = '$2a$10$394j2/GNOR2jpagThC4RWOCkDm2HrM4Mb5nCBrkW3D5OTyQKsH4Nu';

IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantId = @TenantId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @RecruiterUserId)
   AND EXISTS (SELECT 1 FROM dbo.Skills WHERE TenantId = @TenantId AND NormalizedName = N'java')
BEGIN
    DECLARE @JavaRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000001';
    DECLARE @DataRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000002';
    DECLARE @DevOpsRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000003';
    DECLARE @ReactClosedRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000004';
    DECLARE @FrontendReviewRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000005';
    DECLARE @HistJavaRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000011';
    DECLARE @HistPaymentsRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000012';
    DECLARE @HistFullStackRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000013';
    DECLARE @HistApiRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000014';
    DECLARE @HistQaRequestId UNIQUEIDENTIFIER = '24000000-0000-0000-0000-000000000015';

    DECLARE @JavaPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000001';
    DECLARE @DevOpsPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000003';
    DECLARE @ReactClosedPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000004';
    DECLARE @FrontendReviewPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000005';
    DECLARE @HistJavaPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000011';
    DECLARE @HistPaymentsPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000012';
    DECLARE @HistFullStackPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000013';
    DECLARE @HistApiPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000014';
    DECLARE @HistQaPostId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000015';

    DECLARE @JavaAssignmentId UNIQUEIDENTIFIER = '27000000-0000-0000-0000-000000000001';
    DECLARE @DataPmoAssignmentId UNIQUEIDENTIFIER = '27000000-0000-0000-0000-000000000002';
    DECLARE @DevOpsAssignmentId UNIQUEIDENTIFIER = '27000000-0000-0000-0000-000000000003';
    DECLARE @FrontendHmAssignmentId UNIQUEIDENTIFIER = '27000000-0000-0000-0000-000000000005';

    DECLARE @JavaEmployeeId UNIQUEIDENTIFIER = '28000000-0000-0000-0000-000000000001';
    DECLARE @ReactEmployeeId UNIQUEIDENTIFIER = '28000000-0000-0000-0000-000000000002';
    DECLARE @DataEmployeeId UNIQUEIDENTIFIER = '28000000-0000-0000-0000-000000000003';
    DECLARE @DevOpsEmployeeId UNIQUEIDENTIFIER = '28000000-0000-0000-0000-000000000004';

    DECLARE @FarahUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000101';
    DECLARE @ImranUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000102';
    DECLARE @SanaUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000103';
    DECLARE @RazaUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000104';
    DECLARE @KamranUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000105';
    DECLARE @NadiaUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000106';
    DECLARE @AmaraUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000107';
    DECLARE @BilalUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000108';
    DECLARE @HiraUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000109';
    DECLARE @MariamUserId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000110';

    DECLARE @FarahCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000001';
    DECLARE @ImranCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000002';
    DECLARE @SanaCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000003';
    DECLARE @RazaCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000004';
    DECLARE @KamranCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000005';
    DECLARE @NadiaCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000006';
    DECLARE @AmaraCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000007';
    DECLARE @BilalCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000008';
    DECLARE @HiraCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000009';
    DECLARE @MariamCandidateId UNIQUEIDENTIFIER = '23000000-0000-0000-0000-000000000010';

    DECLARE @FarahHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000001';
    DECLARE @ImranHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000002';
    DECLARE @SanaHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000003';
    DECLARE @RazaHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000004';
    DECLARE @KamranHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000005';
    DECLARE @NadiaHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000006';
    DECLARE @AmaraApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000101';
    DECLARE @BilalApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000102';
    DECLARE @HiraApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000103';
    DECLARE @MariamJoinedApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000104';

    DECLARE @JavaScreeningRoundId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000101';
    DECLARE @JavaTechnicalRoundId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000102';
    DECLARE @JavaHodRoundId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000103';
    DECLARE @FrontendScreeningRoundId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000501';
    DECLARE @FrontendTechnicalRoundId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000502';
    DECLARE @FrontendHodRoundId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000503';

    DECLARE @Skills TABLE (Name NVARCHAR(160) NOT NULL PRIMARY KEY, SkillId UNIQUEIDENTIFIER NOT NULL);
    INSERT INTO @Skills (Name, SkillId)
    SELECT NormalizedName, SkillId
    FROM dbo.Skills
    WHERE TenantId = @TenantId
      AND NormalizedName IN
      (
          N'java', N'spring boot', N'microservices', N'api design', N'system design', N'clean architecture',
          N'sql', N'postgresql', N'kafka', N'docker', N'kubernetes', N'aws', N'azure',
          N'react', N'typescript', N'javascript', N'angular', N'python', N'spark', N'airflow',
          N'terraform', N'github actions', N'qa automation', N'playwright', N'ci/cd', N'redis'
      );

    MERGE dbo.CandidateSourceLabels AS target
    USING (VALUES
        (@SourceLinkedInId, @TenantId, N'LinkedInManual', N'LinkedIn', N'External sourcing', N'Active'),
        (@SourceIndeedId, @TenantId, N'IndeedManual', N'Indeed', N'External sourcing', N'Active'),
        (@SourceReferralId, @TenantId, N'Referral', N'Referral', N'Referral reporting', N'Active'),
        (@SourceOtherId, @TenantId, N'Other', N'Other', N'Manual review', N'Active'),
        (@SourceJobPortalId, @TenantId, N'JobPortal', N'Job Portal', N'Talent Pilot portal', N'Active')
    ) AS source (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status)
        ON target.TenantId = source.TenantId AND target.Code = source.Code
    WHEN MATCHED THEN
        UPDATE SET DisplayName = source.DisplayName, ReportingCategory = source.ReportingCategory, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateSourceLabelId, source.TenantId, source.Code, source.DisplayName, source.ReportingCategory, source.Status, @Now, @Now);

    SET @SourceLinkedInId = (SELECT CandidateSourceLabelId FROM dbo.CandidateSourceLabels WHERE TenantId = @TenantId AND Code = N'LinkedInManual');
    SET @SourceIndeedId = (SELECT CandidateSourceLabelId FROM dbo.CandidateSourceLabels WHERE TenantId = @TenantId AND Code = N'IndeedManual');
    SET @SourceReferralId = (SELECT CandidateSourceLabelId FROM dbo.CandidateSourceLabels WHERE TenantId = @TenantId AND Code = N'Referral');
    SET @SourceOtherId = (SELECT CandidateSourceLabelId FROM dbo.CandidateSourceLabels WHERE TenantId = @TenantId AND Code = N'Other');
    SET @SourceJobPortalId = (SELECT CandidateSourceLabelId FROM dbo.CandidateSourceLabels WHERE TenantId = @TenantId AND Code = N'JobPortal');

    ;WITH RequestSource AS
    (
        SELECT *
        FROM (VALUES
            (@JavaRequestId, N'TP-DEMO-101', N'Senior Java Backend Engineer', N'AZAQ Saudia Arabia', @EngineeringDepartmentId, @LahoreLocationId, N'Sourcing', N'Published', N'SOURCING', @JavaAssignmentId, 5.0, 8.0, N'High', 3, 0, @RecruiterUserId, DATEADD(DAY, -22, @Now), N'Senior Java Backend Engineer. Role Summary: Build high-throughput Java services for a Lahore product team. Responsibilities include API design, microservices, Kafka integrations, SQL optimization, and clean architecture.'),
            (@DataRequestId, N'TP-DEMO-102', N'Data Platform Engineer', N'FinEdge Analytics', @EngineeringDepartmentId, @RemoteLocationId, N'PMOReview', N'NotPublished', N'PMO_REVIEW', @DataPmoAssignmentId, 4.0, 7.0, N'Medium', 2, 0, @PresalesUserId, DATEADD(DAY, -8, @Now), N'Data Platform Engineer for batch and stream processing using Python, Spark, Airflow, SQL, Azure, and governance-ready data pipelines.'),
            (@DevOpsRequestId, N'TP-DEMO-103', N'DevOps Platform Engineer', N'CloudVista', @DevOpsDepartmentId, @RemoteLocationId, N'Sourcing', N'Published', N'SOURCING', @DevOpsAssignmentId, 4.0, 7.0, N'High', 1, 0, @RecruiterUserId, DATEADD(DAY, -17, @Now), N'DevOps Platform Engineer for Kubernetes, Terraform, Docker, AWS, Azure, monitoring, and CI/CD reliability.'),
            (@ReactClosedRequestId, N'TP-DEMO-104', N'Senior React Developer', N'Relia', @EngineeringDepartmentId, @LahoreLocationId, N'Closed', N'Unpublished', N'CLOSED', NULL, 5.0, 8.0, N'Medium', 1, 1, @RecruiterUserId, DATEADD(DAY, -60, @Now), N'Senior React Developer request closed after a joined external candidate. Useful for time-to-fill, source quality, offer, and fulfillment analytics.'),
            (@FrontendReviewRequestId, N'TP-DEMO-105', N'Frontend Architect', N'Northstar Digital', @EngineeringDepartmentId, @RemoteLocationId, N'HiringManagerReview', N'Published', N'HIRING_MANAGER_REVIEW', @FrontendHmAssignmentId, 7.0, 10.0, N'Critical', 1, 0, @RecruiterUserId, DATEADD(DAY, -11, @Now), N'Frontend Architect candidate packet is ready for Hiring Manager Review after interview completion.')
        ) AS v(JobRequestId, RequestCode, Title, ClientName, DepartmentId, LocationId, Status, PublishStatus, CurrentStageKey, CurrentAssignmentId, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, CreatedByUserId, CreatedAtUtc, Description)
    )
    MERGE dbo.JobRequests AS target
    USING RequestSource AS source
        ON target.JobRequestId = source.JobRequestId
    WHEN MATCHED THEN
        UPDATE SET Title = source.Title, Description = source.Description, ClientName = source.ClientName, DepartmentId = source.DepartmentId, LocationId = source.LocationId, ExperienceMinYears = source.ExperienceMinYears, ExperienceMaxYears = source.ExperienceMaxYears, Priority = source.Priority, RequiredPositions = source.RequiredPositions, FulfilledPositions = source.FulfilledPositions, Status = source.Status, PublishStatus = source.PublishStatus, CurrentStageKey = source.CurrentStageKey, CurrentAssignmentId = source.CurrentAssignmentId, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobRequestId, TenantId, RequestCode, Title, Description, ClientName, DepartmentId, LocationId, EmploymentType, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, Status, PublishStatus, HiringManagerUserId, CreatedByUserId, CurrentStageKey, CurrentAssignmentId, PublishedAtUtc, ClosedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.JobRequestId, @TenantId, source.RequestCode, source.Title, source.Description, source.ClientName, source.DepartmentId, source.LocationId, N'FullTime', source.ExperienceMinYears, source.ExperienceMaxYears, source.Priority, source.RequiredPositions, source.FulfilledPositions, source.Status, source.PublishStatus, @HiringManagerUserId, source.CreatedByUserId, source.CurrentStageKey, source.CurrentAssignmentId, CASE WHEN source.PublishStatus = N'Published' THEN DATEADD(DAY, 1, source.CreatedAtUtc) ELSE NULL END, CASE WHEN source.Status = N'Closed' THEN DATEADD(DAY, 32, source.CreatedAtUtc) ELSE NULL END, source.CreatedAtUtc, @Now);

    ;WITH HistoricalRequestSource AS
    (
        SELECT *
        FROM (VALUES
            (@HistJavaRequestId, N'TP-HIST-101', N'Java Platform Engineer', N'AZAQ Saudia Arabia', @EngineeringDepartmentId, @LahoreLocationId, N'Closed', 5.0, 8.0, DATEADD(DAY, -190, @Now), N'Historical Java platform role used for Priority 1 rediscovery.'),
            (@HistPaymentsRequestId, N'TP-HIST-102', N'Payments Backend Engineer', N'PayBridge', @EngineeringDepartmentId, @RemoteLocationId, N'Closed', 4.0, 7.0, DATEADD(DAY, -150, @Now), N'Historical payments backend role used for Priority 2 rediscovery.'),
            (@HistFullStackRequestId, N'TP-HIST-103', N'Full Stack Portal Engineer', N'Relia', @EngineeringDepartmentId, @LahoreLocationId, N'Closed', 5.0, 8.0, DATEADD(DAY, -130, @Now), N'Historical full stack role used for Priority 3 rediscovery.'),
            (@HistApiRequestId, N'TP-HIST-104', N'Java API Engineer', N'Northstar Digital', @EngineeringDepartmentId, @RemoteLocationId, N'Closed', 3.0, 6.0, DATEADD(DAY, -95, @Now), N'Historical Java API role used for Priority 4 rediscovery.'),
            (@HistQaRequestId, N'TP-HIST-105', N'QA Automation Engineer', N'Relia', @EngineeringDepartmentId, @RemoteLocationId, N'Closed', 3.0, 5.0, DATEADD(DAY, -75, @Now), N'Historical QA role used for exclusion and source analytics.')
        ) AS v(JobRequestId, RequestCode, Title, ClientName, DepartmentId, LocationId, Status, ExperienceMinYears, ExperienceMaxYears, CreatedAtUtc, Description)
    )
    MERGE dbo.JobRequests AS target
    USING HistoricalRequestSource AS source
        ON target.JobRequestId = source.JobRequestId
    WHEN MATCHED THEN
        UPDATE SET Title = source.Title, Description = source.Description, ClientName = source.ClientName, DepartmentId = source.DepartmentId, LocationId = source.LocationId, ExperienceMinYears = source.ExperienceMinYears, ExperienceMaxYears = source.ExperienceMaxYears, Status = source.Status, PublishStatus = N'Unpublished', CurrentStageKey = N'CLOSED', FulfilledPositions = 0, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobRequestId, TenantId, RequestCode, Title, Description, ClientName, DepartmentId, LocationId, EmploymentType, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, Status, PublishStatus, HiringManagerUserId, CreatedByUserId, CurrentStageKey, CurrentAssignmentId, ClosedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.JobRequestId, @TenantId, source.RequestCode, source.Title, source.Description, source.ClientName, source.DepartmentId, source.LocationId, N'FullTime', source.ExperienceMinYears, source.ExperienceMaxYears, N'Medium', 1, 0, source.Status, N'Unpublished', @HiringManagerUserId, @RecruiterUserId, N'CLOSED', NULL, DATEADD(DAY, 35, source.CreatedAtUtc), source.CreatedAtUtc, @Now);

    ;WITH RequestSkillSource AS
    (
        SELECT requestMap.JobRequestId, skills.SkillId, skillMap.Weight
        FROM (VALUES
            (@JavaRequestId, N'java', 10), (@JavaRequestId, N'spring boot', 9), (@JavaRequestId, N'microservices', 9), (@JavaRequestId, N'api design', 8), (@JavaRequestId, N'system design', 8), (@JavaRequestId, N'kafka', 7), (@JavaRequestId, N'sql', 7),
            (@DataRequestId, N'python', 10), (@DataRequestId, N'spark', 10), (@DataRequestId, N'airflow', 8), (@DataRequestId, N'sql', 8), (@DataRequestId, N'azure', 7),
            (@DevOpsRequestId, N'docker', 8), (@DevOpsRequestId, N'kubernetes', 10), (@DevOpsRequestId, N'terraform', 9), (@DevOpsRequestId, N'aws', 8), (@DevOpsRequestId, N'ci/cd', 8),
            (@FrontendReviewRequestId, N'react', 10), (@FrontendReviewRequestId, N'typescript', 9), (@FrontendReviewRequestId, N'javascript', 8), (@FrontendReviewRequestId, N'api design', 6),
            (@HistJavaRequestId, N'java', 10), (@HistJavaRequestId, N'spring boot', 9), (@HistJavaRequestId, N'microservices', 8),
            (@HistPaymentsRequestId, N'java', 8), (@HistPaymentsRequestId, N'spring boot', 7), (@HistPaymentsRequestId, N'kafka', 7),
            (@HistFullStackRequestId, N'react', 7), (@HistFullStackRequestId, N'java', 7), (@HistFullStackRequestId, N'spring boot', 7),
            (@HistApiRequestId, N'java', 9), (@HistApiRequestId, N'api design', 8), (@HistApiRequestId, N'sql', 7),
            (@HistQaRequestId, N'qa automation', 10), (@HistQaRequestId, N'playwright', 8)
        ) AS skillMap(JobRequestId, SkillName, Weight)
        INNER JOIN @Skills AS skills ON skills.Name = skillMap.SkillName
        CROSS APPLY (SELECT skillMap.JobRequestId) AS requestMap
    )
    MERGE dbo.JobRequestSkills AS target
    USING RequestSkillSource AS source
        ON target.TenantId = @TenantId AND target.JobRequestId = source.JobRequestId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET Weight = source.Weight, IsRequired = CAST(1 AS BIT)
    WHEN NOT MATCHED THEN
        INSERT (TenantId, JobRequestId, SkillId, IsRequired, Weight)
        VALUES (@TenantId, source.JobRequestId, source.SkillId, CAST(1 AS BIT), source.Weight);

    ;WITH AssignmentSource AS
    (
        SELECT *
        FROM (VALUES
            (@JavaAssignmentId, @StageSourcingId, @JavaRequestId, NULL, @RecruitingGroupId, N'Claimed', @RecruiterUserId, DATEADD(DAY, -20, @Now), DATEADD(DAY, -19, @Now), NULL),
            (@DataPmoAssignmentId, @StagePmoReviewId, @DataRequestId, NULL, @PmoEngineeringGroupId, N'Pending', NULL, DATEADD(DAY, -8, @Now), NULL, NULL),
            (@DevOpsAssignmentId, @StageSourcingId, @DevOpsRequestId, NULL, @RecruitingGroupId, N'Claimed', @RecruiterUserId, DATEADD(DAY, -16, @Now), DATEADD(DAY, -16, @Now), NULL),
            (@FrontendHmAssignmentId, @StageHiringManagerId, @FrontendReviewRequestId, @HiringManagerUserId, NULL, N'Pending', NULL, DATEADD(DAY, -2, @Now), NULL, NULL)
        ) AS v(WorkflowAssignmentId, WorkflowStageId, EntityId, AssignedToUserId, AssignedToGroupId, AssignmentStatus, ClaimedByUserId, AssignedAtUtc, ClaimedAtUtc, CompletedAtUtc)
    )
    MERGE dbo.WorkflowAssignments AS target
    USING AssignmentSource AS source
        ON target.WorkflowAssignmentId = source.WorkflowAssignmentId
    WHEN MATCHED THEN
        UPDATE SET WorkflowStageId = source.WorkflowStageId, EntityId = source.EntityId, AssignedToUserId = source.AssignedToUserId, AssignedToGroupId = source.AssignedToGroupId, AssignmentStatus = source.AssignmentStatus, ClaimedByUserId = source.ClaimedByUserId, AssignedAtUtc = source.AssignedAtUtc, ClaimedAtUtc = source.ClaimedAtUtc, CompletedAtUtc = source.CompletedAtUtc
    WHEN NOT MATCHED THEN
        INSERT (WorkflowAssignmentId, TenantId, WorkflowDefinitionId, WorkflowStageId, EntityType, EntityId, AssignedToUserId, AssignedToGroupId, AssignmentStatus, ClaimedByUserId, AssignedAtUtc, ClaimedAtUtc, CompletedAtUtc)
        VALUES (source.WorkflowAssignmentId, @TenantId, @WorkflowDefinitionId, source.WorkflowStageId, N'JobRequest', source.EntityId, source.AssignedToUserId, source.AssignedToGroupId, source.AssignmentStatus, source.ClaimedByUserId, source.AssignedAtUtc, source.ClaimedAtUtc, source.CompletedAtUtc);

    ;WITH PostSource AS
    (
        SELECT *
        FROM (VALUES
            (@JavaPostId, @JavaRequestId, N'Senior Java Backend Engineer', N'Published', @EngineeringDepartmentId, @LahoreLocationId, 5.0, 8.0, 3, DATEADD(DAY, -18, @Now), NULL),
            (@DevOpsPostId, @DevOpsRequestId, N'DevOps Platform Engineer', N'Published', @DevOpsDepartmentId, @RemoteLocationId, 4.0, 7.0, 1, DATEADD(DAY, -14, @Now), NULL),
            (@ReactClosedPostId, @ReactClosedRequestId, N'Senior React Developer', N'Closed', @EngineeringDepartmentId, @LahoreLocationId, 5.0, 8.0, 1, DATEADD(DAY, -57, @Now), DATEADD(DAY, -26, @Now)),
            (@FrontendReviewPostId, @FrontendReviewRequestId, N'Frontend Architect', N'Published', @EngineeringDepartmentId, @RemoteLocationId, 7.0, 10.0, 1, DATEADD(DAY, -9, @Now), NULL),
            (@HistJavaPostId, @HistJavaRequestId, N'Java Platform Engineer', N'Closed', @EngineeringDepartmentId, @LahoreLocationId, 5.0, 8.0, 1, DATEADD(DAY, -185, @Now), DATEADD(DAY, -154, @Now)),
            (@HistPaymentsPostId, @HistPaymentsRequestId, N'Payments Backend Engineer', N'Closed', @EngineeringDepartmentId, @RemoteLocationId, 4.0, 7.0, 1, DATEADD(DAY, -145, @Now), DATEADD(DAY, -113, @Now)),
            (@HistFullStackPostId, @HistFullStackRequestId, N'Full Stack Portal Engineer', N'Closed', @EngineeringDepartmentId, @LahoreLocationId, 5.0, 8.0, 1, DATEADD(DAY, -126, @Now), DATEADD(DAY, -91, @Now)),
            (@HistApiPostId, @HistApiRequestId, N'Java API Engineer', N'Closed', @EngineeringDepartmentId, @RemoteLocationId, 3.0, 6.0, 1, DATEADD(DAY, -91, @Now), DATEADD(DAY, -61, @Now)),
            (@HistQaPostId, @HistQaRequestId, N'QA Automation Engineer', N'Closed', @EngineeringDepartmentId, @RemoteLocationId, 3.0, 5.0, 1, DATEADD(DAY, -70, @Now), DATEADD(DAY, -35, @Now))
        ) AS v(JobPostId, JobRequestId, Title, Status, DepartmentId, LocationId, ExperienceMinYears, ExperienceMaxYears, RequiredPositions, PublishedAtUtc, ClosedAtUtc)
    )
    MERGE dbo.JobPosts AS target
    USING PostSource AS source
        ON target.JobPostId = source.JobPostId
    WHEN MATCHED THEN
        UPDATE SET Title = source.Title, DepartmentId = source.DepartmentId, LocationId = source.LocationId, ExperienceMinYears = source.ExperienceMinYears, ExperienceMaxYears = source.ExperienceMaxYears, RequiredPositions = source.RequiredPositions, Status = source.Status, PublishedAtUtc = source.PublishedAtUtc, ClosedAtUtc = source.ClosedAtUtc, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobPostId, TenantId, JobRequestId, RecruiterOwnerUserId, Title, Description, DepartmentId, LocationId, ExperienceMinYears, ExperienceMaxYears, RequiredPositions, Status, PublishedAtUtc, ClosedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.JobPostId, @TenantId, source.JobRequestId, @RecruiterUserId, source.Title, CONCAT(source.Title, N' portal posting generated from the linked Job Request. Candidates can apply through Talent Pilot while recruiters retain manual decision control.'), source.DepartmentId, source.LocationId, source.ExperienceMinYears, source.ExperienceMaxYears, source.RequiredPositions, source.Status, source.PublishedAtUtc, source.ClosedAtUtc, DATEADD(DAY, -1, source.PublishedAtUtc), @Now);

    ;WITH PostSkillSource AS
    (
        SELECT skillMap.JobPostId, skills.SkillId, skillMap.Weight
        FROM (VALUES
            (@JavaPostId, N'java', 10), (@JavaPostId, N'spring boot', 9), (@JavaPostId, N'microservices', 9), (@JavaPostId, N'kafka', 8), (@JavaPostId, N'sql', 7), (@JavaPostId, N'api design', 8),
            (@DevOpsPostId, N'kubernetes', 10), (@DevOpsPostId, N'terraform', 9), (@DevOpsPostId, N'docker', 8), (@DevOpsPostId, N'aws', 8),
            (@ReactClosedPostId, N'react', 10), (@ReactClosedPostId, N'typescript', 8), (@ReactClosedPostId, N'javascript', 8),
            (@FrontendReviewPostId, N'react', 10), (@FrontendReviewPostId, N'typescript', 9), (@FrontendReviewPostId, N'api design', 6),
            (@HistJavaPostId, N'java', 10), (@HistJavaPostId, N'spring boot', 9), (@HistJavaPostId, N'microservices', 8),
            (@HistPaymentsPostId, N'java', 8), (@HistPaymentsPostId, N'spring boot', 7), (@HistPaymentsPostId, N'kafka', 7),
            (@HistFullStackPostId, N'react', 7), (@HistFullStackPostId, N'java', 7), (@HistFullStackPostId, N'spring boot', 7),
            (@HistApiPostId, N'java', 9), (@HistApiPostId, N'api design', 8), (@HistApiPostId, N'sql', 7),
            (@HistQaPostId, N'qa automation', 10), (@HistQaPostId, N'playwright', 8)
        ) AS skillMap(JobPostId, SkillName, Weight)
        INNER JOIN @Skills AS skills ON skills.Name = skillMap.SkillName
    )
    MERGE dbo.JobPostSkills AS target
    USING PostSkillSource AS source
        ON target.TenantId = @TenantId AND target.JobPostId = source.JobPostId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET Weight = source.Weight, IsRequired = CAST(1 AS BIT)
    WHEN NOT MATCHED THEN
        INSERT (TenantId, JobPostId, SkillId, IsRequired, Weight)
        VALUES (@TenantId, source.JobPostId, source.SkillId, CAST(1 AS BIT), source.Weight);

    ;WITH RoundSource AS
    (
        SELECT *
        FROM (VALUES
            (@JavaScreeningRoundId, @JavaPostId, @ScreeningTemplateRoundId, 1, N'HR Screening', @RecruiterUserId, 30, N'Active'),
            (@JavaTechnicalRoundId, @JavaPostId, @TechnicalTemplateRoundId, 2, N'Technical Interview', @InterviewerUserId, 60, N'Active'),
            (@JavaHodRoundId, @JavaPostId, @HodTemplateRoundId, 3, N'Department Head Interview', @HodUserId, 45, N'Active'),
            (@FrontendScreeningRoundId, @FrontendReviewPostId, @ScreeningTemplateRoundId, 1, N'HR Screening', @RecruiterUserId, 30, N'Active'),
            (@FrontendTechnicalRoundId, @FrontendReviewPostId, @TechnicalTemplateRoundId, 2, N'Technical Interview', @InterviewerUserId, 60, N'Active'),
            (@FrontendHodRoundId, @FrontendReviewPostId, @HodTemplateRoundId, 3, N'Department Head Interview', @HodUserId, 45, N'Active')
        ) AS v(JobPostInterviewRoundId, JobPostId, InterviewTemplateRoundId, RoundOrder, Name, OwnerUserId, DurationMinutes, Status)
    )
    MERGE dbo.JobPostInterviewRounds AS target
    USING RoundSource AS source
        ON target.JobPostInterviewRoundId = source.JobPostInterviewRoundId
    WHEN MATCHED THEN
        UPDATE SET JobPostId = source.JobPostId, InterviewTemplateRoundId = source.InterviewTemplateRoundId, RoundOrder = source.RoundOrder, Name = source.Name, OwnerUserId = source.OwnerUserId, DurationMinutes = source.DurationMinutes, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobPostInterviewRoundId, TenantId, JobPostId, InterviewTemplateRoundId, RoundOrder, Name, OwnerUserId, DurationMinutes, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.JobPostInterviewRoundId, @TenantId, source.JobPostId, source.InterviewTemplateRoundId, source.RoundOrder, source.Name, source.OwnerUserId, source.DurationMinutes, source.Status, @Now, @Now);

    ;WITH ProjectSource AS
    (
        SELECT *
        FROM (VALUES
            ('29000000-0000-0000-0000-000000000001', N'AZAQ-PAY', N'AZAQ Payment Modernization', N'AZAQ Saudia Arabia', @EngineeringDepartmentId, N'Closed', CONVERT(date, '2024-01-15'), CONVERT(date, '2025-02-28')),
            ('29000000-0000-0000-0000-000000000002', N'RELIA-OPS', N'Relia Operations Portal', N'Relia', @EngineeringDepartmentId, N'Closed', CONVERT(date, '2023-05-01'), CONVERT(date, '2024-09-30')),
            ('29000000-0000-0000-0000-000000000003', N'FINEDGE-LAKE', N'FinEdge Lakehouse', N'FinEdge Analytics', @EngineeringDepartmentId, N'Active', CONVERT(date, '2025-01-01'), NULL),
            ('29000000-0000-0000-0000-000000000004', N'CLOUDVISTA-AUTO', N'CloudVista Automation Platform', N'CloudVista', @DevOpsDepartmentId, N'Active', CONVERT(date, '2024-07-01'), NULL)
        ) AS v(ProjectId, Code, Name, ClientName, DepartmentId, Status, StartsOn, EndsOn)
    )
    MERGE dbo.Projects AS target
    USING ProjectSource AS source
        ON target.ProjectId = source.ProjectId
    WHEN MATCHED THEN
        UPDATE SET Code = source.Code, Name = source.Name, ClientName = source.ClientName, DepartmentId = source.DepartmentId, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.ProjectId, @TenantId, source.DepartmentId, source.Code, source.Name, source.ClientName, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);

    ;WITH EmployeeSource AS
    (
        SELECT *
        FROM (VALUES
            (@JavaEmployeeId, N'EMP-DEMO-101', N'Zain Javaid', N'zain.javaid@8pkk57.onmicrosoft.com', @EngineeringDepartmentId, @LahoreLocationId, N'Senior Java Engineer', 6.8, CONVERT(date, '2021-02-15'), N'Available', N'Benched', N'Active'),
            (@ReactEmployeeId, N'EMP-DEMO-102', N'Mehwish Tariq', N'mehwish.tariq@8pkk57.onmicrosoft.com', @EngineeringDepartmentId, @LahoreLocationId, N'Frontend Engineer', 5.4, CONVERT(date, '2022-06-20'), N'Available', N'Benched', N'Active'),
            (@DataEmployeeId, N'EMP-DEMO-103', N'Hira Batool', N'hira.batool@8pkk57.onmicrosoft.com', @EngineeringDepartmentId, @RemoteLocationId, N'Data Engineer', 4.9, CONVERT(date, '2021-11-10'), N'PartiallyAllocated', N'PartialBench', N'Active'),
            (@DevOpsEmployeeId, N'EMP-DEMO-104', N'Farhan Iqbal', N'farhan.iqbal@8pkk57.onmicrosoft.com', @DevOpsDepartmentId, @RemoteLocationId, N'DevOps Engineer', 7.1, CONVERT(date, '2020-08-01'), N'Available', N'Benched', N'Active')
        ) AS v(EmployeeId, EmployeeCode, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, JoiningDate, AvailabilityStatus, BenchStatus, Status)
    )
    MERGE dbo.Employees AS target
    USING EmployeeSource AS source
        ON target.EmployeeId = source.EmployeeId
    WHEN MATCHED THEN
        UPDATE SET DisplayName = source.DisplayName, Email = source.Email, DepartmentId = source.DepartmentId, LocationId = source.LocationId, Designation = source.Designation, ExperienceYears = source.ExperienceYears, JoiningDate = source.JoiningDate, AvailabilityStatus = source.AvailabilityStatus, BenchStatus = source.BenchStatus, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (EmployeeId, TenantId, AppUserId, EmployeeCode, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, JoiningDate, AvailabilityStatus, BenchStatus, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.EmployeeId, @TenantId, NULL, source.EmployeeCode, source.DisplayName, source.Email, source.DepartmentId, source.LocationId, source.Designation, source.ExperienceYears, source.JoiningDate, source.AvailabilityStatus, source.BenchStatus, source.Status, @Now, @Now);

    ;WITH EmployeeSkillSource AS
    (
        SELECT employeeMap.EmployeeId, skills.SkillId, employeeMap.SkillLevel, employeeMap.YearsExperience, employeeMap.IsPrimary
        FROM (VALUES
            (@JavaEmployeeId, N'java', N'Advanced', 6.5, 1), (@JavaEmployeeId, N'spring boot', N'Advanced', 5.5, 1), (@JavaEmployeeId, N'microservices', N'Advanced', 5.0, 1), (@JavaEmployeeId, N'kafka', N'Intermediate', 3.5, 0), (@JavaEmployeeId, N'sql', N'Advanced', 6.0, 0),
            (@ReactEmployeeId, N'react', N'Advanced', 5.0, 1), (@ReactEmployeeId, N'typescript', N'Advanced', 4.5, 1), (@ReactEmployeeId, N'api design', N'Intermediate', 2.0, 0),
            (@DataEmployeeId, N'python', N'Advanced', 4.5, 1), (@DataEmployeeId, N'spark', N'Advanced', 3.5, 1), (@DataEmployeeId, N'airflow', N'Intermediate', 2.5, 0), (@DataEmployeeId, N'azure', N'Intermediate', 3.0, 0),
            (@DevOpsEmployeeId, N'kubernetes', N'Advanced', 5.0, 1), (@DevOpsEmployeeId, N'terraform', N'Advanced', 4.5, 1), (@DevOpsEmployeeId, N'docker', N'Advanced', 6.0, 1), (@DevOpsEmployeeId, N'aws', N'Advanced', 5.5, 0)
        ) AS employeeMap(EmployeeId, SkillName, SkillLevel, YearsExperience, IsPrimary)
        INNER JOIN @Skills AS skills ON skills.Name = employeeMap.SkillName
    )
    MERGE dbo.EmployeeSkills AS target
    USING EmployeeSkillSource AS source
        ON target.TenantId = @TenantId AND target.EmployeeId = source.EmployeeId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
    WHEN NOT MATCHED THEN
        INSERT (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary)
        VALUES (@TenantId, source.EmployeeId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary);

    ;WITH EmployeeProjectSource AS
    (
        SELECT *
        FROM (VALUES
            ('29100000-0000-0000-0000-000000000001', @JavaEmployeeId, '29000000-0000-0000-0000-000000000001', 100, N'Completed', CONVERT(date, '2024-01-15'), CONVERT(date, '2025-02-28')),
            ('29100000-0000-0000-0000-000000000002', @ReactEmployeeId, '29000000-0000-0000-0000-000000000002', 100, N'Completed', CONVERT(date, '2023-05-01'), CONVERT(date, '2024-09-30')),
            ('29100000-0000-0000-0000-000000000003', @DataEmployeeId, '29000000-0000-0000-0000-000000000003', 50, N'Active', CONVERT(date, '2025-01-01'), NULL),
            ('29100000-0000-0000-0000-000000000004', @DevOpsEmployeeId, '29000000-0000-0000-0000-000000000004', 60, N'Active', CONVERT(date, '2024-07-01'), NULL)
        ) AS v(ProjectAssignmentId, EmployeeId, ProjectId, AllocationPercent, Status, StartsOn, EndsOn)
    )
    MERGE dbo.EmployeeProjectAssignments AS target
    USING EmployeeProjectSource AS source
        ON target.ProjectAssignmentId = source.ProjectAssignmentId
    WHEN MATCHED THEN
        UPDATE SET AllocationPercent = source.AllocationPercent, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (ProjectAssignmentId, TenantId, EmployeeId, ProjectId, AllocationPercent, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.ProjectAssignmentId, @TenantId, source.EmployeeId, source.ProjectId, source.AllocationPercent, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);

    ;WITH UserSource AS
    (
        SELECT *
        FROM (VALUES
            (@FarahUserId, N'Farah Qureshi', N'farah.qureshi@8pkk57.onmicrosoft.com', N'FQ'),
            (@ImranUserId, N'Imran Malik', N'imran.malik@8pkk57.onmicrosoft.com', N'IM'),
            (@SanaUserId, N'Sana Javed', N'sana.javed@8pkk57.onmicrosoft.com', N'SJ'),
            (@RazaUserId, N'Raza Ahmed', N'raza.ahmed@8pkk57.onmicrosoft.com', N'RA'),
            (@KamranUserId, N'Kamran Hired', N'kamran.hired@8pkk57.onmicrosoft.com', N'KH'),
            (@NadiaUserId, N'Nadia Inactive', N'nadia.inactive@8pkk57.onmicrosoft.com', N'NI'),
            (@AmaraUserId, N'Amara Haq', N'amara.haq@8pkk57.onmicrosoft.com', N'AH'),
            (@BilalUserId, N'Bilal Tariq', N'bilal.tariq@8pkk57.onmicrosoft.com', N'BT'),
            (@HiraUserId, N'Hira Saleem', N'hira.saleem@8pkk57.onmicrosoft.com', N'HS'),
            (@MariamUserId, N'Mariam Siddiqui', N'mariam.siddiqui@8pkk57.onmicrosoft.com', N'MS')
        ) AS v(UserId, DisplayName, Email, Initials)
    )
    MERGE dbo.AppUsers AS target
    USING UserSource AS source
        ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET DisplayName = source.DisplayName, Email = source.Email, EmailNormalized = UPPER(source.Email), Initials = source.Initials, AccountStatus = N'Active', DeletedAtUtc = NULL, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, LastActiveAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.UserId, @TenantId, source.DisplayName, source.Email, UPPER(source.Email), source.Initials, N'Active', DATEADD(DAY, -2, @Now), @Now, @Now);

    ;WITH CredentialSource AS
    (
        SELECT *
        FROM (VALUES
            ('23100000-0000-0000-0000-000000000101', @FarahUserId),
            ('23100000-0000-0000-0000-000000000102', @ImranUserId),
            ('23100000-0000-0000-0000-000000000103', @SanaUserId),
            ('23100000-0000-0000-0000-000000000104', @RazaUserId),
            ('23100000-0000-0000-0000-000000000105', @KamranUserId),
            ('23100000-0000-0000-0000-000000000106', @NadiaUserId),
            ('23100000-0000-0000-0000-000000000107', @AmaraUserId),
            ('23100000-0000-0000-0000-000000000108', @BilalUserId),
            ('23100000-0000-0000-0000-000000000109', @HiraUserId),
            ('23100000-0000-0000-0000-000000000110', @MariamUserId)
        ) AS v(UserCredentialId, UserId)
    )
    MERGE dbo.UserCredentials AS target
    USING CredentialSource AS source
        ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET PasswordHash = @DemoPasswordHash, PasswordUpdatedAtUtc = @Now, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserCredentialId, TenantId, UserId, PasswordHash, PasswordUpdatedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.UserCredentialId, @TenantId, source.UserId, @DemoPasswordHash, @Now, @Now, @Now);

    ;WITH UserRoleSource AS
    (
        SELECT UserId FROM (VALUES (@FarahUserId), (@ImranUserId), (@SanaUserId), (@RazaUserId), (@KamranUserId), (@NadiaUserId), (@AmaraUserId), (@BilalUserId), (@HiraUserId), (@MariamUserId)) AS v(UserId)
    )
    MERGE dbo.UserRoles AS target
    USING UserRoleSource AS source
        ON target.TenantId = @TenantId AND target.UserId = source.UserId AND target.RoleId = @CandidateRoleId
    WHEN NOT MATCHED THEN
        INSERT (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
        VALUES (@TenantId, source.UserId, @CandidateRoleId, @TenantAdminUserId, @Now);

    ;WITH CandidateSource AS
    (
        SELECT *
        FROM (VALUES
            (@FarahCandidateId, @FarahUserId, N'Farah Qureshi', N'farah.qureshi@8pkk57.onmicrosoft.com', N'+92-300-1010101', N'https://linkedin.com/in/farah-qureshi-seed', N'Senior Java Developer', N'Product Studio', 7.2, 20, N'Active'),
            (@ImranCandidateId, @ImranUserId, N'Imran Malik', N'imran.malik@8pkk57.onmicrosoft.com', N'+92-300-1010102', N'https://linkedin.com/in/imran-malik-seed', N'Backend Engineer', N'Careem Labs', 5.8, 30, N'Active'),
            (@SanaCandidateId, @SanaUserId, N'Sana Javed', N'sana.javed@8pkk57.onmicrosoft.com', N'+92-300-1010103', N'https://linkedin.com/in/sana-javed-seed', N'Full Stack Engineer', N'Northstar Digital', 6.4, 15, N'Active'),
            (@RazaCandidateId, @RazaUserId, N'Raza Ahmed', N'raza.ahmed@8pkk57.onmicrosoft.com', N'+92-300-1010104', N'https://linkedin.com/in/raza-ahmed-seed', N'Java API Developer', N'CodeSmiths', 4.8, 30, N'Active'),
            (@KamranCandidateId, @KamranUserId, N'Kamran Hired', N'kamran.hired@8pkk57.onmicrosoft.com', N'+92-300-1010105', N'https://linkedin.com/in/kamran-hired-seed', N'Senior Backend Engineer', N'EnterpriseWorks', 8.0, 0, N'Hired'),
            (@NadiaCandidateId, @NadiaUserId, N'Nadia Inactive', N'nadia.inactive@8pkk57.onmicrosoft.com', N'+92-300-1010106', N'https://linkedin.com/in/nadia-inactive-seed', N'Backend Engineer', N'LegacySoft', 5.0, 45, N'Inactive'),
            (@AmaraCandidateId, @AmaraUserId, N'Amara Haq', N'amara.haq@8pkk57.onmicrosoft.com', N'+92-300-1010107', N'https://linkedin.com/in/amara-haq-seed', N'Java Backend Engineer', N'Product Studio', 6.8, 15, N'Active'),
            (@BilalCandidateId, @BilalUserId, N'Bilal Tariq', N'bilal.tariq@8pkk57.onmicrosoft.com', N'+92-300-1010108', N'https://linkedin.com/in/bilal-tariq-seed', N'Backend Engineer', N'FinEdge', 5.4, 30, N'Active'),
            (@HiraCandidateId, @HiraUserId, N'Hira Saleem', N'hira.saleem@8pkk57.onmicrosoft.com', N'+92-300-1010109', N'https://linkedin.com/in/hira-saleem-seed', N'Data Engineer', N'DataWorks', 4.6, 30, N'Active'),
            (@MariamCandidateId, @MariamUserId, N'Mariam Siddiqui', N'mariam.siddiqui@8pkk57.onmicrosoft.com', N'+92-300-1010110', N'https://linkedin.com/in/mariam-siddiqui-seed', N'Senior React Developer', N'Relia', 6.5, 15, N'Active')
        ) AS v(CandidateId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, NoticePeriodDays, Status)
    )
    MERGE dbo.Candidates AS target
    USING CandidateSource AS source
        ON target.CandidateId = source.CandidateId
    WHEN MATCHED THEN
        UPDATE SET DisplayName = source.DisplayName, Email = source.Email, Phone = source.Phone, LinkedInUrl = source.LinkedInUrl, CurrentDesignation = source.CurrentDesignation, CurrentCompany = source.CurrentCompany, ExperienceYears = source.ExperienceYears, NoticePeriodDays = source.NoticePeriodDays, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateId, TenantId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, NoticePeriodDays, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateId, @TenantId, source.AppUserId, source.DisplayName, source.Email, source.Phone, source.LinkedInUrl, source.CurrentDesignation, source.CurrentCompany, source.ExperienceYears, source.NoticePeriodDays, source.Status, @Now, @Now);

    ;WITH CandidateEducationSource AS
    (
        SELECT *
        FROM (VALUES
            ('23200000-0000-0000-0000-000000000001', @FarahCandidateId, N'FAST NUCES', N'BS Computer Science', 2017),
            ('23200000-0000-0000-0000-000000000002', @ImranCandidateId, N'UET Lahore', N'BS Software Engineering', 2019),
            ('23200000-0000-0000-0000-000000000003', @SanaCandidateId, N'Punjab University', N'BS Computer Science', 2018),
            ('23200000-0000-0000-0000-000000000004', @RazaCandidateId, N'COMSATS', N'BS Software Engineering', 2020),
            ('23200000-0000-0000-0000-000000000007', @AmaraCandidateId, N'FAST NUCES', N'BS Computer Science', 2018),
            ('23200000-0000-0000-0000-000000000008', @BilalCandidateId, N'UET Lahore', N'BS Computer Engineering', 2019),
            ('23200000-0000-0000-0000-000000000009', @HiraCandidateId, N'LUMS', N'MS Data Science', 2021),
            ('23200000-0000-0000-0000-000000000010', @MariamCandidateId, N'FAST NUCES', N'BS Computer Science', 2018)
        ) AS v(CandidateEducationId, CandidateId, UniversityName, DegreeName, GraduationYear)
    )
    MERGE dbo.CandidateEducation AS target
    USING CandidateEducationSource AS source
        ON target.CandidateEducationId = source.CandidateEducationId
    WHEN MATCHED THEN
        UPDATE SET UniversityName = source.UniversityName, DegreeName = source.DegreeName, GraduationYear = source.GraduationYear, IsPrimary = CAST(1 AS BIT), UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateEducationId, TenantId, CandidateId, UniversityName, DegreeName, GraduationYear, IsPrimary, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateEducationId, @TenantId, source.CandidateId, source.UniversityName, source.DegreeName, source.GraduationYear, CAST(1 AS BIT), @Now, @Now);

    ;WITH CandidateWorkSource AS
    (
        SELECT *
        FROM (VALUES
            ('23300000-0000-0000-0000-000000000001', @FarahCandidateId, N'Product Studio', N'Senior Java Developer', 1, CONVERT(date, '2021-03-01'), NULL),
            ('23300000-0000-0000-0000-000000000002', @ImranCandidateId, N'Careem Labs', N'Backend Engineer', 1, CONVERT(date, '2020-06-01'), NULL),
            ('23300000-0000-0000-0000-000000000003', @SanaCandidateId, N'Northstar Digital', N'Full Stack Engineer', 1, CONVERT(date, '2019-02-01'), NULL),
            ('23300000-0000-0000-0000-000000000004', @RazaCandidateId, N'CodeSmiths', N'Java API Developer', 1, CONVERT(date, '2021-01-01'), NULL),
            ('23300000-0000-0000-0000-000000000007', @AmaraCandidateId, N'Product Studio', N'Java Backend Engineer', 1, CONVERT(date, '2020-04-01'), NULL),
            ('23300000-0000-0000-0000-000000000008', @BilalCandidateId, N'FinEdge', N'Backend Engineer', 1, CONVERT(date, '2020-09-01'), NULL),
            ('23300000-0000-0000-0000-000000000009', @HiraCandidateId, N'DataWorks', N'Data Engineer', 1, CONVERT(date, '2021-08-01'), NULL),
            ('23300000-0000-0000-0000-000000000010', @MariamCandidateId, N'Relia', N'Senior React Developer', 1, CONVERT(date, '2020-05-01'), NULL)
        ) AS v(CandidateWorkHistoryId, CandidateId, CompanyName, Title, IsCurrent, StartsOn, EndsOn)
    )
    MERGE dbo.CandidateWorkHistory AS target
    USING CandidateWorkSource AS source
        ON target.CandidateWorkHistoryId = source.CandidateWorkHistoryId
    WHEN MATCHED THEN
        UPDATE SET CompanyName = source.CompanyName, Title = source.Title, IsCurrent = source.IsCurrent, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateWorkHistoryId, TenantId, CandidateId, CompanyName, Title, IsCurrent, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateWorkHistoryId, @TenantId, source.CandidateId, source.CompanyName, source.Title, source.IsCurrent, source.StartsOn, source.EndsOn, @Now, @Now);

    ;WITH CandidateSkillSource AS
    (
        SELECT candidateMap.CandidateId, skills.SkillId, candidateMap.SkillLevel, candidateMap.YearsExperience, candidateMap.IsPrimary
        FROM (VALUES
            (@FarahCandidateId, N'java', N'Advanced', 7.0, 1), (@FarahCandidateId, N'spring boot', N'Advanced', 6.0, 1), (@FarahCandidateId, N'microservices', N'Advanced', 5.5, 1), (@FarahCandidateId, N'kafka', N'Intermediate', 3.0, 0),
            (@ImranCandidateId, N'java', N'Advanced', 5.0, 1), (@ImranCandidateId, N'spring boot', N'Intermediate', 4.0, 1), (@ImranCandidateId, N'kafka', N'Intermediate', 3.0, 0), (@ImranCandidateId, N'sql', N'Advanced', 5.0, 0),
            (@SanaCandidateId, N'java', N'Intermediate', 4.0, 1), (@SanaCandidateId, N'react', N'Advanced', 5.0, 1), (@SanaCandidateId, N'spring boot', N'Intermediate', 3.0, 0), (@SanaCandidateId, N'typescript', N'Advanced', 4.0, 0),
            (@RazaCandidateId, N'java', N'Advanced', 4.5, 1), (@RazaCandidateId, N'api design', N'Advanced', 4.0, 1), (@RazaCandidateId, N'sql', N'Intermediate', 3.0, 0),
            (@KamranCandidateId, N'java', N'Advanced', 8.0, 1), (@KamranCandidateId, N'microservices', N'Advanced', 6.0, 1),
            (@NadiaCandidateId, N'java', N'Intermediate', 4.0, 1), (@NadiaCandidateId, N'spring boot', N'Intermediate', 3.0, 0),
            (@AmaraCandidateId, N'java', N'Advanced', 6.5, 1), (@AmaraCandidateId, N'spring boot', N'Advanced', 5.5, 1), (@AmaraCandidateId, N'microservices', N'Advanced', 5.0, 1), (@AmaraCandidateId, N'kafka', N'Intermediate', 3.0, 0), (@AmaraCandidateId, N'sql', N'Advanced', 6.0, 0),
            (@BilalCandidateId, N'java', N'Intermediate', 4.5, 1), (@BilalCandidateId, N'spring boot', N'Intermediate', 4.0, 1), (@BilalCandidateId, N'kafka', N'Advanced', 4.0, 0), (@BilalCandidateId, N'postgresql', N'Intermediate', 3.0, 0),
            (@HiraCandidateId, N'python', N'Advanced', 4.5, 1), (@HiraCandidateId, N'spark', N'Advanced', 3.0, 1), (@HiraCandidateId, N'sql', N'Advanced', 4.0, 0), (@HiraCandidateId, N'java', N'Beginner', 1.5, 0),
            (@MariamCandidateId, N'react', N'Advanced', 6.0, 1), (@MariamCandidateId, N'typescript', N'Advanced', 5.0, 1), (@MariamCandidateId, N'javascript', N'Advanced', 6.0, 0)
        ) AS candidateMap(CandidateId, SkillName, SkillLevel, YearsExperience, IsPrimary)
        INNER JOIN @Skills AS skills ON skills.Name = candidateMap.SkillName
    )
    MERGE dbo.CandidateSkills AS target
    USING CandidateSkillSource AS source
        ON target.TenantId = @TenantId AND target.CandidateId = source.CandidateId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
    WHEN NOT MATCHED THEN
        INSERT (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary)
        VALUES (@TenantId, source.CandidateId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary);

    DELETE feedback
    FROM dbo.InterviewFeedback AS feedback
    INNER JOIN dbo.Interviews AS interview ON interview.InterviewId = feedback.InterviewId
    WHERE interview.JobApplicationId = @BilalApplicationId;

    DELETE participant
    FROM dbo.InterviewParticipants AS participant
    INNER JOIN dbo.Interviews AS interview ON interview.InterviewId = participant.InterviewId
    WHERE interview.JobApplicationId = @BilalApplicationId;

    DELETE FROM dbo.Interviews WHERE JobApplicationId = @BilalApplicationId;
    DELETE FROM dbo.JobApplicationDocuments WHERE JobApplicationId = @BilalApplicationId;
    DELETE FROM dbo.JobApplicationStatusHistory WHERE JobApplicationId = @BilalApplicationId;
    DELETE FROM dbo.JobRequestFulfillments WHERE JobApplicationId = @BilalApplicationId;
    DELETE FROM dbo.OfferPresentationMeetings WHERE JobApplicationId = @BilalApplicationId;
    DELETE FROM dbo.OfferLetters WHERE JobApplicationId = @BilalApplicationId;
    DELETE FROM dbo.AiRecommendationLogs WHERE AiRecommendationLogId = '26800000-0000-0000-0000-000000000008' OR RecommendedEntityId = @BilalApplicationId OR SourceEntityId = @BilalApplicationId;
    DELETE FROM dbo.AiAgentRuns WHERE SourceEntityId = @BilalApplicationId;
    DELETE FROM dbo.VectorEmbeddings WHERE EntityId = @BilalApplicationId;
    DELETE FROM dbo.JobApplications WHERE JobApplicationId = @BilalApplicationId;

    ;WITH ApplicationSource AS
    (
        SELECT *
        FROM (VALUES
            (@FarahHistApplicationId, @HistJavaRequestId, @HistJavaPostId, @FarahCandidateId, @SourceReferralId, N'Referral', N'OnHold', 0, 0, DATEADD(DAY, -183, @Now), DATEADD(DAY, -155, @Now), N'Cleared all interviews; client paused hiring and asked to keep the profile warm.', NULL, N'Employee referral', N'Priority 1 seed: all interviews passed and kept on hold.'),
            (@ImranHistApplicationId, @HistPaymentsRequestId, @HistPaymentsPostId, @ImranCandidateId, @SourceLinkedInId, N'LinkedIn', N'Rejected', 0, 0, DATEADD(DAY, -143, @Now), DATEADD(DAY, -115, @Now), N'Client selected an internal transfer after positive backend interview feedback.', N'https://linkedin.com/in/imran-malik-seed', N'LinkedIn Recruiter', N'Priority 2 seed: passed at least half of interviews for a similar backend role.'),
            (@SanaHistApplicationId, @HistFullStackRequestId, @HistFullStackPostId, @SanaCandidateId, @SourceIndeedId, N'Indeed', N'OfferDeclined', 0, 0, DATEADD(DAY, -123, @Now), DATEADD(DAY, -93, @Now), N'Candidate cleared all interviews, received an offer, and then accepted a counter offer.', NULL, N'Indeed outbound search', N'Priority 3 seed: late-stage non-fit closure with positive interview evidence and offer declined for timing/compensation reasons.'),
            (@RazaHistApplicationId, @HistApiRequestId, @HistApiPostId, @RazaCandidateId, @SourceOtherId, N'Other', N'Withdrawn', 0, 0, DATEADD(DAY, -88, @Now), DATEADD(DAY, -70, @Now), N'Candidate became unavailable before interviews; profile remains a strong semantic match.', NULL, N'Community referral', N'Priority 4 seed: strong skill match with limited interview evidence.'),
            (@KamranHistApplicationId, @HistApiRequestId, @HistApiPostId, @KamranCandidateId, @SourceLinkedInId, N'LinkedIn', N'Hired', 0, 0, DATEADD(DAY, -86, @Now), DATEADD(DAY, -60, @Now), N'Hired by another team; should be excluded from rediscovery.', NULL, N'LinkedIn Recruiter', N'Exclusion seed: hired candidates should not appear.'),
            (@NadiaHistApplicationId, @HistPaymentsRequestId, @HistPaymentsPostId, @NadiaCandidateId, @SourceReferralId, N'Referral', N'OnHold', 0, 0, DATEADD(DAY, -84, @Now), DATEADD(DAY, -66, @Now), N'Inactive profile; should be excluded from rediscovery.', NULL, N'Referral', N'Exclusion seed: inactive candidate should not appear.'),
            (@AmaraApplicationId, @JavaRequestId, @JavaPostId, @AmaraCandidateId, @SourceJobPortalId, N'Job Portal', N'Applied', 1, 0, DATEADD(DAY, -3, @Now), NULL, NULL, NULL, N'Talent Pilot portal', N'Current applicant: strong Java backend fit with complete cover letter and CV evidence.'),
            (@HiraApplicationId, @JavaRequestId, @JavaPostId, @HiraCandidateId, @SourceJobPortalId, N'Job Portal', N'Screening', 1, 0, DATEADD(DAY, -1, @Now), NULL, NULL, NULL, N'Talent Pilot portal', N'Current applicant: partial Java fit but strong data engineering evidence.'),
            (@MariamJoinedApplicationId, @ReactClosedRequestId, @ReactClosedPostId, @MariamCandidateId, @SourceJobPortalId, N'Job Portal', N'Joined', 0, 0, DATEADD(DAY, -55, @Now), DATEADD(DAY, -27, @Now), N'Joined after accepted offer.', NULL, N'Talent Pilot portal', N'Fulfillment seed: joined external candidate for closed React request.')
        ) AS v(JobApplicationId, JobRequestId, JobPostId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, IsActive, IsInvited, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason, SourceUrl, SourceDetail, RecruiterNotes)
    )
    MERGE dbo.JobApplications AS target
    USING ApplicationSource AS source
        ON target.JobApplicationId = source.JobApplicationId
    WHEN MATCHED THEN
        UPDATE SET JobRequestId = source.JobRequestId, JobPostId = source.JobPostId, CandidateId = source.CandidateId, CandidateSourceLabelId = source.CandidateSourceLabelId, SourceLabel = source.SourceLabel, CurrentStatus = source.CurrentStatus, IsActive = source.IsActive, IsInvited = source.IsInvited, AppliedAtUtc = source.AppliedAtUtc, FinalDecisionAtUtc = source.FinalDecisionAtUtc, FinalDecisionReason = source.FinalDecisionReason, SourceUrl = source.SourceUrl, SourceDetail = source.SourceDetail, AddedByUserId = CASE WHEN source.IsInvited = 1 THEN @RecruiterUserId ELSE NULL END, RecruiterNotes = source.RecruiterNotes, CoverLetterText = CASE WHEN source.JobApplicationId IN (@AmaraApplicationId, @HiraApplicationId) THEN CONCAT(N'I am interested in ', (SELECT Title FROM dbo.JobPosts WHERE JobPostId = source.JobPostId), N' and can bring relevant delivery experience to the team.') ELSE CoverLetterText END, ApplicationSnapshotJson = CONCAT(N'{"seed":true,"jobPostId":"', CONVERT(NVARCHAR(36), source.JobPostId), N'","source":"', source.SourceLabel, N'"}'), UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobApplicationId, TenantId, JobRequestId, JobPostId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason, SourceDetail, SourceUrl, AddedByUserId, RecruiterNotes, CoverLetterText, ApplicationSnapshotJson, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.JobApplicationId, @TenantId, source.JobRequestId, source.JobPostId, source.CandidateId, source.CandidateSourceLabelId, source.SourceLabel, source.CurrentStatus, 1, source.IsActive, source.IsInvited, CASE WHEN source.IsInvited = 1 THEN NULL ELSE source.AppliedAtUtc END, source.AppliedAtUtc, source.FinalDecisionAtUtc, source.FinalDecisionReason, source.SourceDetail, source.SourceUrl, CASE WHEN source.IsInvited = 1 THEN @RecruiterUserId ELSE NULL END, source.RecruiterNotes, CASE WHEN source.JobApplicationId IN (@AmaraApplicationId, @HiraApplicationId) THEN CONCAT(N'I am interested in ', (SELECT Title FROM dbo.JobPosts WHERE JobPostId = source.JobPostId), N' and can bring relevant delivery experience to the team.') ELSE NULL END, CONCAT(N'{"seed":true,"jobPostId":"', CONVERT(NVARCHAR(36), source.JobPostId), N'","source":"', source.SourceLabel, N'"}'), source.AppliedAtUtc, @Now);

    ;WITH DocumentSource AS
    (
        SELECT *
        FROM (VALUES
            ('26400000-0000-0000-0000-000000000001', @FarahHistApplicationId, @FarahCandidateId, N'CV', N'Farah_Qureshi_Java_Backend.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 235520, N'LocalFileSystem', N'applications/farah-qureshi-java-backend.docx', N'local-app-documents', N'1111111111111111111111111111111111111111111111111111111111111111', @FarahUserId, N'Extracted', N'Farah Qureshi - Senior Java Developer. Core evidence: Java, Spring Boot, Kafka, microservices, SQL, PostgreSQL, API design, system design, production backend ownership, and enterprise integration. This profile has no React, JavaScript, TypeScript, HTML, CSS, or frontend portal delivery evidence. Historical interviews were for backend Java platform work.', N'docx-wordprocessingml-v1'),
            ('26400000-0000-0000-0000-000000000002', @ImranHistApplicationId, @ImranCandidateId, N'CV', N'Imran_Malik_Backend_Payments.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 221184, N'LocalFileSystem', N'applications/imran-malik-backend-payments.docx', N'local-app-documents', N'2222222222222222222222222222222222222222222222222222222222222222', @ImranUserId, N'Extracted', N'Imran Malik - Backend API Engineer. Java, Spring Boot, payment services, PostgreSQL, Kafka, Redis, REST APIs, observability, and CI/CD. Prior interviews focused on payments backend reliability and system design. No direct React or JavaScript frontend implementation evidence.', N'docx-wordprocessingml-v1'),
            ('26400000-0000-0000-0000-000000000003', @SanaHistApplicationId, @SanaCandidateId, N'CV', N'Sana_Javed_Full_Stack_Portal.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 241664, N'LocalFileSystem', N'applications/sana-javed-full-stack-portal.docx', N'local-app-documents', N'3333333333333333333333333333333333333333333333333333333333333333', @SanaUserId, N'Extracted', N'Sana Javed - Full Stack Portal Engineer. React, TypeScript, JavaScript, component design, REST API integration, accessibility fixes, frontend performance work, plus Java and Spring Boot collaboration. Prior interview feedback confirmed React and TypeScript portal delivery with acceptable backend collaboration depth.', N'docx-wordprocessingml-v1'),
            ('26400000-0000-0000-0000-000000000004', @RazaHistApplicationId, @RazaCandidateId, N'CV', N'Raza_Naqvi_Java_API.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 184320, N'LocalFileSystem', N'applications/raza-naqvi-java-api.docx', N'local-app-documents', N'4444444444444444444444444444444444444444444444444444444444444444', @RazaUserId, N'Extracted', N'Raza Naqvi - Java API Engineer. Java, REST API design, SQL, Redis, Docker, service observability, and clean architecture. Limited interview evidence and no frontend React, JavaScript, TypeScript, or CSS evidence.', N'docx-wordprocessingml-v1'),
            ('26400000-0000-0000-0000-000000000005', @MariamJoinedApplicationId, @MariamCandidateId, N'CV', N'Mariam_Siddiqui_React_Developer.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 250880, N'LocalFileSystem', N'applications/mariam-siddiqui-react-developer.docx', N'local-app-documents', N'5555555555555555555555555555555555555555555555555555555555555555', @MariamUserId, N'Extracted', N'Mariam Siddiqui - Senior React Developer. React, TypeScript, JavaScript, design-system components, Azure portal delivery, web performance, accessibility, and frontend architecture. Joined TKXEL after a completed React hiring process and should not appear in active rediscovery pools.', N'docx-wordprocessingml-v1'),
            ('26400000-0000-0000-0000-000000000101', @AmaraApplicationId, @AmaraCandidateId, N'CV', N'Amara_Haq_Java_Backend.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 245760, N'LocalFileSystem', N'applications/amara-haq-java-backend.docx', N'local-app-documents', N'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', @AmaraUserId, N'Extracted', N'Amara Haq - Senior Java Backend Engineer. Seven years building Java, Spring Boot, microservices, Kafka, PostgreSQL, REST APIs, API design, system design, Docker, and Kubernetes services for banking and marketplace platforms. Backend-focused profile with no React, Angular, JavaScript, TypeScript, CSS, or frontend portal delivery evidence.', N'docx-wordprocessingml-v1'),
            ('26400000-0000-0000-0000-000000000103', @HiraApplicationId, @HiraCandidateId, N'CV', N'Hira_Saleem_Data.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 198656, N'LocalFileSystem', N'applications/hira-saleem-data.docx', N'local-app-documents', N'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc', @HiraUserId, N'Extracted', N'Hira Saleem - Data Engineer. Python, Spark, SQL, Airflow, data pipelines, ETL, lakehouse modeling, and dashboard data quality work. Has limited Java exposure through data platform services, but no React, JavaScript, TypeScript, or frontend product engineering evidence.', N'docx-wordprocessingml-v1')
        ) AS v(ApplicationDocumentId, JobApplicationId, CandidateId, DocumentType, OriginalFileName, ContentType, SizeBytes, StorageProvider, StorageKey, StorageContainer, ContentHashSha256, UploadedByUserId, ExtractionStatus, ExtractedText, ParserVersion)
    )
    MERGE dbo.JobApplicationDocuments AS target
    USING DocumentSource AS source
        ON target.ApplicationDocumentId = source.ApplicationDocumentId
    WHEN MATCHED THEN
        UPDATE SET DocumentType = source.DocumentType, OriginalFileName = source.OriginalFileName, ContentType = source.ContentType, SizeBytes = source.SizeBytes, StorageProvider = source.StorageProvider, StorageKey = source.StorageKey, StorageContainer = source.StorageContainer, ContentHashSha256 = source.ContentHashSha256, ExtractionStatus = source.ExtractionStatus, ExtractedText = source.ExtractedText, ExtractedTextHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), source.ExtractedText)), 2)), ParserVersion = source.ParserVersion, ExtractedAtUtc = @Now, ExtractionError = NULL, Status = N'Active', UploadedByUserId = source.UploadedByUserId, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (ApplicationDocumentId, TenantId, JobApplicationId, CandidateId, DocumentType, OriginalFileName, ContentType, SizeBytes, StorageProvider, StorageKey, StorageContainer, ContentHashSha256, ExtractionStatus, ExtractedText, ExtractedTextHashSha256, ParserVersion, ExtractedAtUtc, ExtractionError, Status, UploadedByUserId, UploadedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.ApplicationDocumentId, @TenantId, source.JobApplicationId, source.CandidateId, source.DocumentType, source.OriginalFileName, source.ContentType, source.SizeBytes, source.StorageProvider, source.StorageKey, source.StorageContainer, source.ContentHashSha256, source.ExtractionStatus, source.ExtractedText, LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), source.ExtractedText)), 2)), source.ParserVersion, @Now, NULL, N'Active', source.UploadedByUserId, @Now, @Now, @Now);

    ;WITH InterviewSource AS
    (
        SELECT *
        FROM (VALUES
            ('26100000-0000-0000-0000-000000000001', @FarahHistApplicationId, NULL, @RecruiterUserId, @RecruiterUserId, DATEADD(DAY, -180, @Now), 30, N'Completed'),
            ('26100000-0000-0000-0000-000000000002', @FarahHistApplicationId, NULL, @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -176, @Now), 60, N'Completed'),
            ('26100000-0000-0000-0000-000000000003', @FarahHistApplicationId, NULL, @HodUserId, @RecruiterUserId, DATEADD(DAY, -171, @Now), 45, N'Completed'),
            ('26100000-0000-0000-0000-000000000004', @ImranHistApplicationId, NULL, @RecruiterUserId, @RecruiterUserId, DATEADD(DAY, -140, @Now), 30, N'Completed'),
            ('26100000-0000-0000-0000-000000000005', @ImranHistApplicationId, NULL, @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -136, @Now), 60, N'Completed'),
            ('26100000-0000-0000-0000-000000000006', @ImranHistApplicationId, NULL, @HodUserId, @RecruiterUserId, DATEADD(DAY, -132, @Now), 45, N'Completed'),
            ('26100000-0000-0000-0000-000000000007', @SanaHistApplicationId, NULL, @RecruiterUserId, @RecruiterUserId, DATEADD(DAY, -120, @Now), 30, N'Completed'),
            ('26100000-0000-0000-0000-000000000008', @SanaHistApplicationId, NULL, @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -116, @Now), 60, N'Completed'),
            ('26100000-0000-0000-0000-000000000009', @SanaHistApplicationId, NULL, @HodUserId, @RecruiterUserId, DATEADD(DAY, -112, @Now), 45, N'Completed'),
            ('26100000-0000-0000-0000-000000000010', @MariamJoinedApplicationId, @FrontendScreeningRoundId, @RecruiterUserId, @RecruiterUserId, DATEADD(DAY, -51, @Now), 30, N'Completed'),
            ('26100000-0000-0000-0000-000000000011', @MariamJoinedApplicationId, @FrontendTechnicalRoundId, @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -47, @Now), 60, N'Completed'),
            ('26100000-0000-0000-0000-000000000012', @MariamJoinedApplicationId, @FrontendHodRoundId, @HodUserId, @RecruiterUserId, DATEADD(DAY, -43, @Now), 45, N'Completed'),
            ('26100000-0000-0000-0000-000000000101', @AmaraApplicationId, @JavaScreeningRoundId, @RecruiterUserId, @RecruiterUserId, DATEADD(DAY, 2, @Now), 30, N'Scheduled')
        ) AS v(InterviewId, JobApplicationId, JobPostInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, Status)
    )
    MERGE dbo.Interviews AS target
    USING InterviewSource AS source
        ON target.InterviewId = source.InterviewId
    WHEN MATCHED THEN
        UPDATE SET JobApplicationId = source.JobApplicationId, JobPostInterviewRoundId = source.JobPostInterviewRoundId, InterviewerUserId = source.InterviewerUserId, ScheduledByUserId = source.ScheduledByUserId, StartsAtUtc = source.StartsAtUtc, DurationMinutes = source.DurationMinutes, MeetingLink = N'https://meet.example.test/talent-pilot-seed', LocationText = CASE WHEN source.Status = N'Scheduled' THEN N'Microsoft Teams' ELSE N'Talent Pilot Interview Room' END, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (InterviewId, TenantId, JobApplicationId, JobRequestInterviewRoundId, JobPostInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, MeetingLink, LocationText, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.InterviewId, @TenantId, source.JobApplicationId, NULL, source.JobPostInterviewRoundId, source.InterviewerUserId, source.ScheduledByUserId, source.StartsAtUtc, source.DurationMinutes, N'https://meet.example.test/talent-pilot-seed', CASE WHEN source.Status = N'Scheduled' THEN N'Microsoft Teams' ELSE N'Talent Pilot Interview Room' END, source.Status, @Now, @Now);

    ;WITH FeedbackSource AS
    (
        SELECT *
        FROM (VALUES
            ('26200000-0000-0000-0000-000000000001', '26100000-0000-0000-0000-000000000001', @RecruiterUserId, 4, 5, 5, N'Proceed', N'Clear communication and strong Java backend ownership.'),
            ('26200000-0000-0000-0000-000000000002', '26100000-0000-0000-0000-000000000002', @InterviewerUserId, 5, 4, 4, N'Proceed', N'Strong microservices, Kafka, and SQL depth.'),
            ('26200000-0000-0000-0000-000000000003', '26100000-0000-0000-0000-000000000003', @HodUserId, 5, 5, 4, N'Proceed', N'Passed department-head discussion; kept warm because the opening was paused.'),
            ('26200000-0000-0000-0000-000000000004', '26100000-0000-0000-0000-000000000004', @RecruiterUserId, 4, 4, 4, N'Proceed', N'Good backend communication and relevant payments context.'),
            ('26200000-0000-0000-0000-000000000005', '26100000-0000-0000-0000-000000000005', @InterviewerUserId, 4, 3, 4, N'Proceed', N'Passed technical with some mentoring needed on system design.'),
            ('26200000-0000-0000-0000-000000000006', '26100000-0000-0000-0000-000000000006', @HodUserId, 2, 3, 3, N'NoHire', N'Not selected by client despite positive earlier rounds.'),
            ('26200000-0000-0000-0000-000000000007', '26100000-0000-0000-0000-000000000007', @RecruiterUserId, 4, 4, 4, N'Proceed', N'Positive recruiter screen for full-stack portal work.'),
            ('26200000-0000-0000-0000-000000000008', '26100000-0000-0000-0000-000000000008', @InterviewerUserId, 4, 4, 4, N'Proceed', N'Cleared the technical interview for full-stack portal delivery; strongest evidence was React and TypeScript with acceptable backend collaboration depth.'),
            ('26200000-0000-0000-0000-000000000009', '26100000-0000-0000-0000-000000000009', @HodUserId, 4, 4, 4, N'Proceed', N'Cleared department-head discussion and was approved for offer; candidate later accepted a counter offer.'),
            ('26200000-0000-0000-0000-000000000010', '26100000-0000-0000-0000-000000000010', @RecruiterUserId, 5, 5, 5, N'Proceed', N'Excellent screen for React role.'),
            ('26200000-0000-0000-0000-000000000011', '26100000-0000-0000-0000-000000000011', @InterviewerUserId, 5, 4, 5, N'Proceed', N'Strong React and performance optimization evidence.'),
            ('26200000-0000-0000-0000-000000000012', '26100000-0000-0000-0000-000000000012', @HodUserId, 5, 5, 5, N'Proceed', N'Approved by department head; offer accepted.')
        ) AS v(InterviewFeedbackId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText)
    )
    MERGE dbo.InterviewFeedback AS target
    USING FeedbackSource AS source
        ON target.InterviewFeedbackId = source.InterviewFeedbackId
    WHEN MATCHED THEN
        UPDATE SET SubmittedByUserId = source.SubmittedByUserId, TechnicalScore = source.TechnicalScore, CommunicationScore = source.CommunicationScore, CultureScore = source.CultureScore, Recommendation = source.Recommendation, FeedbackText = source.FeedbackText, IsSubmitted = CAST(1 AS BIT), SubmittedAtUtc = @Now, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (InterviewFeedbackId, TenantId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText, IsSubmitted, SubmittedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.InterviewFeedbackId, @TenantId, source.InterviewId, source.SubmittedByUserId, source.TechnicalScore, source.CommunicationScore, source.CultureScore, source.Recommendation, source.FeedbackText, CAST(1 AS BIT), @Now, @Now, @Now);

    ;WITH StatusHistorySource AS
    (
        SELECT *
        FROM (VALUES
            ('26300000-0000-0000-0000-000000000001', @AmaraApplicationId, NULL, N'Applied', @AmaraUserId, DATEADD(DAY, -3, @Now), N'Portal application submitted.'),
            ('26300000-0000-0000-0000-000000000003', @HiraApplicationId, NULL, N'Applied', @HiraUserId, DATEADD(DAY, -1, @Now), N'Portal application submitted.'),
            ('26300000-0000-0000-0000-000000000004', @MariamJoinedApplicationId, N'Offered', N'Joined', @HiringManagerUserId, DATEADD(DAY, -27, @Now), N'Candidate joined and fulfilled the request.'),
            ('26300000-0000-0000-0000-000000000005', @FarahHistApplicationId, N'Interviewing', N'OnHold', @RecruiterUserId, DATEADD(DAY, -155, @Now), N'All interviews cleared; kept on hold.')
        ) AS v(JobApplicationStatusHistoryId, JobApplicationId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc, Notes)
    )
    MERGE dbo.JobApplicationStatusHistory AS target
    USING StatusHistorySource AS source
        ON target.JobApplicationStatusHistoryId = source.JobApplicationStatusHistoryId
    WHEN MATCHED THEN
        UPDATE SET FromStatus = source.FromStatus, ToStatus = source.ToStatus, ChangedByUserId = source.ChangedByUserId, ChangedAtUtc = source.ChangedAtUtc, Notes = source.Notes
    WHEN NOT MATCHED THEN
        INSERT (JobApplicationStatusHistoryId, TenantId, JobApplicationId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc, Notes)
        VALUES (source.JobApplicationStatusHistoryId, @TenantId, source.JobApplicationId, source.FromStatus, source.ToStatus, source.ChangedByUserId, source.ChangedAtUtc, source.Notes);

    ;WITH ReferralSource AS
    (
        SELECT *
        FROM (VALUES
            ('28100000-0000-0000-0000-000000000001', @DataRequestId, @DataEmployeeId, @PmoUserId, @PresalesUserId, N'Referred', 82.50, N'Data engineer has Spark, Python, Azure, and current partial bench availability.', NULL),
            ('28100000-0000-0000-0000-000000000002', @JavaRequestId, @JavaEmployeeId, @PmoUserId, @PresalesUserId, N'AcceptedByPresales', 91.00, N'Java engineer matches Lahore location, Java/Spring Boot skills, and AZAQ project history.', N'Presales accepted as strong internal option for similar Java backend work.'),
            ('28100000-0000-0000-0000-000000000003', @DevOpsRequestId, @DevOpsEmployeeId, @PmoUserId, @PresalesUserId, N'RejectedByPresales', 76.00, N'DevOps engineer matches platform needs but timing was not aligned.', N'Presales requested external recruiter sourcing because immediate availability was uncertain.')
        ) AS v(JobRequestEmployeeReferralId, JobRequestId, EmployeeId, ReferredByUserId, PresalesUserId, Status, FitScore, RecommendationSummary, ClientFeedback)
    )
    MERGE dbo.JobRequestEmployeeReferrals AS target
    USING ReferralSource AS source
        ON target.JobRequestEmployeeReferralId = source.JobRequestEmployeeReferralId
    WHEN MATCHED THEN
        UPDATE SET JobRequestId = source.JobRequestId, EmployeeId = source.EmployeeId, ReferredByUserId = source.ReferredByUserId, PresalesUserId = source.PresalesUserId, Status = source.Status, FitScore = source.FitScore, RecommendationSummary = source.RecommendationSummary, ClientFeedback = source.ClientFeedback, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobRequestEmployeeReferralId, TenantId, JobRequestId, EmployeeId, ReferredByUserId, PresalesUserId, Status, FitScore, RecommendationSummary, ClientFeedback, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.JobRequestEmployeeReferralId, @TenantId, source.JobRequestId, source.EmployeeId, source.ReferredByUserId, source.PresalesUserId, source.Status, source.FitScore, source.RecommendationSummary, source.ClientFeedback, @Now, @Now);

    MERGE dbo.JobRequestFulfillments AS target
    USING (VALUES
        ('28200000-0000-0000-0000-000000000001', @ReactClosedRequestId, NULL, @MariamJoinedApplicationId, NULL, @MariamCandidateId, @HiringManagerUserId, N'ExternalCandidate', N'Completed', DATEADD(DAY, -27, @Now), N'Joined candidate fulfilled the closed React request.')
    ) AS source(JobRequestFulfillmentId, JobRequestId, JobRequestEmployeeReferralId, JobApplicationId, EmployeeId, CandidateId, FulfilledByUserId, FulfillmentType, Status, FulfilledAtUtc, Notes)
        ON target.JobRequestFulfillmentId = source.JobRequestFulfillmentId
    WHEN MATCHED THEN
        UPDATE SET JobRequestId = source.JobRequestId, JobApplicationId = source.JobApplicationId, CandidateId = source.CandidateId, FulfilledByUserId = source.FulfilledByUserId, FulfillmentType = source.FulfillmentType, Status = source.Status, FulfilledAtUtc = source.FulfilledAtUtc, Notes = source.Notes
    WHEN NOT MATCHED THEN
        INSERT (JobRequestFulfillmentId, TenantId, JobRequestId, JobRequestEmployeeReferralId, JobApplicationId, EmployeeId, CandidateId, FulfilledByUserId, FulfillmentType, Status, FulfilledAtUtc, Notes)
        VALUES (source.JobRequestFulfillmentId, @TenantId, source.JobRequestId, source.JobRequestEmployeeReferralId, source.JobApplicationId, source.EmployeeId, source.CandidateId, source.FulfilledByUserId, source.FulfillmentType, source.Status, source.FulfilledAtUtc, source.Notes);

    MERGE dbo.OfferLetters AS target
    USING (VALUES
        ('28300000-0000-0000-0000-000000000001', @MariamJoinedApplicationId, @ReactClosedPostId, @ReactClosedRequestId, @MariamCandidateId, @HiringManagerUserId, 1, N'Accepted', N'PKR market aligned package', CONVERT(date, DATEADD(DAY, -24, @Now)), N'Fatima Noor', N'Lahore', N'Dear Mariam, we are pleased to offer you the Senior React Developer role at TKXEL Careers.')
    ) AS source(OfferLetterId, JobApplicationId, JobPostId, JobRequestId, CandidateId, GeneratedByUserId, Version, Status, CompensationText, StartDate, ReportingManager, WorkLocation, Body)
        ON target.OfferLetterId = source.OfferLetterId
    WHEN MATCHED THEN
        UPDATE SET Status = source.Status, CompensationText = source.CompensationText, StartDate = source.StartDate, ReportingManager = source.ReportingManager, WorkLocation = source.WorkLocation, Body = source.Body, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (OfferLetterId, TenantId, JobApplicationId, JobPostId, JobRequestId, CandidateId, GeneratedByUserId, Version, Status, CompensationText, StartDate, ReportingManager, WorkLocation, Body, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.OfferLetterId, @TenantId, source.JobApplicationId, source.JobPostId, source.JobRequestId, source.CandidateId, source.GeneratedByUserId, source.Version, source.Status, source.CompensationText, source.StartDate, source.ReportingManager, source.WorkLocation, source.Body, DATEADD(DAY, -30, @Now), @Now);

    MERGE dbo.OfferPresentationMeetings AS target
    USING (VALUES
        ('28400000-0000-0000-0000-000000000001', '28300000-0000-0000-0000-000000000001', @MariamJoinedApplicationId, @HiringManagerUserId, DATEADD(DAY, -29, @Now), N'TKXEL Lahore office', N'In-person offer presentation completed.', N'Completed')
    ) AS source(OfferPresentationMeetingId, OfferLetterId, JobApplicationId, ScheduledByUserId, MeetingAtUtc, LocationText, Notes, Status)
        ON target.OfferPresentationMeetingId = source.OfferPresentationMeetingId
    WHEN MATCHED THEN
        UPDATE SET MeetingAtUtc = source.MeetingAtUtc, LocationText = source.LocationText, Notes = source.Notes, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (OfferPresentationMeetingId, TenantId, OfferLetterId, JobApplicationId, ScheduledByUserId, MeetingAtUtc, LocationText, Notes, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.OfferPresentationMeetingId, @TenantId, source.OfferLetterId, source.JobApplicationId, source.ScheduledByUserId, source.MeetingAtUtc, source.LocationText, source.Notes, source.Status, @Now, @Now);

    ;WITH AgentRunSource AS
    (
        SELECT *
        FROM (VALUES
            ('26700000-0000-0000-0000-000000000001', N'requirement-parser', N'JobRequest', @JavaRequestId, N'Succeeded', DATEADD(DAY, -22, @Now), DATEADD(DAY, -22, DATEADD(MINUTE, 1, @Now)), N'Requirement profile indexed for Java backend matching.'),
            ('26700000-0000-0000-0000-000000000002', N'job-description-drafter', N'JobRequest', @JavaRequestId, N'Succeeded', DATEADD(DAY, -22, @Now), DATEADD(DAY, -22, DATEADD(MINUTE, 2, @Now)), N'Drafted editable Java backend job description.'),
            ('26700000-0000-0000-0000-000000000003', N'cv-parser', N'JobApplication', @AmaraApplicationId, N'Succeeded', DATEADD(DAY, -3, @Now), DATEADD(DAY, -3, DATEADD(MINUTE, 1, @Now)), N'Parsed DOCX CV for applicant ranking evidence.'),
            ('26700000-0000-0000-0000-000000000004', N'bench-matching', N'JobRequest', @DataRequestId, N'Succeeded', DATEADD(DAY, -2, @Now), DATEADD(DAY, -2, DATEADD(MINUTE, 2, @Now)), N'Ranked benched employees for Data Platform Engineer PMO review.'),
            ('26700000-0000-0000-0000-000000000005', N'talent-rediscovery', N'JobRequest', @JavaRequestId, N'Succeeded', DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, DATEADD(MINUTE, 2, @Now)), N'Ranked warm historical candidates for Java backend sourcing.'),
            ('26700000-0000-0000-0000-000000000006', N'applicant-ranking', N'JobPost', @JavaPostId, N'Succeeded', DATEADD(HOUR, -8, @Now), DATEADD(HOUR, -8, DATEADD(MINUTE, 2, @Now)), N'Ranked current Java job post applications.'),
            ('26700000-0000-0000-0000-000000000007', N'fit-explanation', N'JobApplication', @AmaraApplicationId, N'Succeeded', DATEADD(HOUR, -7, @Now), DATEADD(HOUR, -7, DATEADD(MINUTE, 1, @Now)), N'Generated fit explanation from applicant-ranking evidence.'),
            ('26700000-0000-0000-0000-000000000008', N'hiring-manager-decision-brief', N'JobApplication', @MariamJoinedApplicationId, N'Succeeded', DATEADD(DAY, -31, @Now), DATEADD(DAY, -31, DATEADD(MINUTE, 2, @Now)), N'Prepared decision brief from complete interview packet.')
        ) AS v(AiAgentRunId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, Status, StartedAtUtc, CompletedAtUtc, OutputSummary)
    )
    MERGE dbo.AiAgentRuns AS target
    USING AgentRunSource AS source
        ON target.AiAgentRunId = source.AiAgentRunId
    WHEN MATCHED THEN
        UPDATE SET AiAgentDefinitionId = source.AiAgentDefinitionId, SourceEntityType = source.SourceEntityType, SourceEntityId = source.SourceEntityId, Status = source.Status, StartedAtUtc = source.StartedAtUtc, CompletedAtUtc = source.CompletedAtUtc, OutputSummary = source.OutputSummary, MetadataJson = N'{"seed":true,"semanticSimilarity":"demo"}'
    WHEN NOT MATCHED THEN
        INSERT (AiAgentRunId, TenantId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, ModelName, EmbeddingModelName, InputHash, OutputSummary, Status, StartedAtUtc, CompletedAtUtc, MetadataJson)
        VALUES (source.AiAgentRunId, @TenantId, source.AiAgentDefinitionId, source.SourceEntityType, source.SourceEntityId, N'mock/ollama', N'nomic-embed-text', CONVERT(NVARCHAR(128), source.AiAgentRunId), source.OutputSummary, source.Status, source.StartedAtUtc, source.CompletedAtUtc, N'{"seed":true,"semanticSimilarity":"demo"}');

    ;WITH RecommendationSource AS
    (
        SELECT *
        FROM (VALUES
            ('26800000-0000-0000-0000-000000000001', N'bench-matching', '26700000-0000-0000-0000-000000000004', N'JobRequest', @DataRequestId, N'Employee', @DataEmployeeId, 86.00, N'Hira Batool is a strong data platform fit with Python, Spark, Airflow, Azure, and partial bench availability.', N'{"rank":1,"fitScore":86,"confidence":"High","matchedSkills":["Python","Spark","Airflow","Azure"],"gaps":["Dedicated availability"],"seed":true}'),
            ('26800000-0000-0000-0000-000000000002', N'bench-matching', '26700000-0000-0000-0000-000000000004', N'JobRequest', @DataRequestId, N'Employee', @JavaEmployeeId, 42.00, N'Zain Javaid is a weaker fit for Data Platform because Java backend experience does not cover Spark and Airflow needs.', N'{"rank":2,"fitScore":42,"confidence":"Low","matchedSkills":["SQL"],"gaps":["Spark","Airflow","Python"],"seed":true}'),
            ('26800000-0000-0000-0000-000000000003', N'talent-rediscovery', '26700000-0000-0000-0000-000000000005', N'JobRequest', @JavaRequestId, N'Candidate', @FarahCandidateId, 93.00, N'Priority 1: cleared all interviews for a similar Java Platform Engineer role and was kept on hold.', N'{"rank":1,"fitScore":93,"confidence":"High","priority":"Priority 1","interviewPassSummary":"3/3 passed","seed":true}'),
            ('26800000-0000-0000-0000-000000000004', N'talent-rediscovery', '26700000-0000-0000-0000-000000000005', N'JobRequest', @JavaRequestId, N'Candidate', @ImranCandidateId, 82.00, N'Priority 2: passed at least half of interviews for a similar backend requirement.', N'{"rank":2,"fitScore":82,"confidence":"High","priority":"Priority 2","interviewPassSummary":"2/3 passed","seed":true}'),
            ('26800000-0000-0000-0000-000000000005', N'talent-rediscovery', '26700000-0000-0000-0000-000000000005', N'JobRequest', @JavaRequestId, N'Candidate', @SanaCandidateId, 73.00, N'Priority 3: late-stage candidate who cleared interviews but declined the offer for timing/compensation reasons.', N'{"rank":3,"fitScore":73,"confidence":"Medium","priority":"Priority 3","interviewPassSummary":"3/3 passed","seed":true}'),
            ('26800000-0000-0000-0000-000000000006', N'talent-rediscovery', '26700000-0000-0000-0000-000000000005', N'JobRequest', @JavaRequestId, N'Candidate', @RazaCandidateId, 68.00, N'Priority 4: strong Java API and SQL match with limited interview evidence.', N'{"rank":4,"fitScore":68,"confidence":"Medium","priority":"Priority 4","interviewPassSummary":"0/0 passed","seed":true}'),
            ('26800000-0000-0000-0000-000000000007', N'applicant-ranking', '26700000-0000-0000-0000-000000000006', N'JobPost', @JavaPostId, N'JobApplication', @AmaraApplicationId, 89.00, N'Amara has the strongest current application fit with Java, Spring Boot, microservices, Kafka, SQL, CV metadata, and a complete cover letter.', N'{"rank":1,"fitScore":89,"confidence":"High","documentEvidence":["CV","Cover letter"],"matchedSkills":["Java","Spring Boot","Microservices","Kafka","SQL"],"seed":true}'),
            ('26800000-0000-0000-0000-000000000009', N'applicant-ranking', '26700000-0000-0000-0000-000000000006', N'JobPost', @JavaPostId, N'JobApplication', @HiraApplicationId, 51.00, N'Hira has strong data engineering evidence but limited Java backend relevance for this post.', N'{"rank":3,"fitScore":51,"confidence":"Low","documentEvidence":["CV"],"matchedSkills":["SQL","Python"],"gaps":["Spring Boot","Microservices"],"seed":true}')
        ) AS v(AiRecommendationLogId, AiAgentDefinitionId, AiAgentRunId, SourceEntityType, SourceEntityId, RecommendedEntityType, RecommendedEntityId, Score, Explanation, PayloadJson)
    )
    MERGE dbo.AiRecommendationLogs AS target
    USING RecommendationSource AS source
        ON target.AiRecommendationLogId = source.AiRecommendationLogId
    WHEN MATCHED THEN
        UPDATE SET AiAgentDefinitionId = source.AiAgentDefinitionId, SourceEntityType = source.SourceEntityType, SourceEntityId = source.SourceEntityId, RecommendedEntityType = source.RecommendedEntityType, RecommendedEntityId = source.RecommendedEntityId, AiAgentRunId = source.AiAgentRunId, Score = source.Score, Explanation = source.Explanation, PayloadJson = source.PayloadJson, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (AiRecommendationLogId, TenantId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, RecommendedEntityType, RecommendedEntityId, AiAgentRunId, Score, Explanation, PayloadJson, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.AiRecommendationLogId, @TenantId, source.AiAgentDefinitionId, source.SourceEntityType, source.SourceEntityId, source.RecommendedEntityType, source.RecommendedEntityId, source.AiAgentRunId, source.Score, source.Explanation, source.PayloadJson, @Now, @Now);
END;
GO
