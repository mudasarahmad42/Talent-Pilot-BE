SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @DemoPasswordHash NVARCHAR(500) = N'$2a$10$394j2/GNOR2jpagThC4RWOCkDm2HrM4Mb5nCBrkW3D5OTyQKsH4Nu';

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

DECLARE @TenantAdminRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222201';
DECLARE @PresalesRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222202';
DECLARE @PmoRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222203';
DECLARE @RecruiterRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222204';
DECLARE @InterviewerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222205';
DECLARE @HiringManagerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222206';
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222207';
DECLARE @HodRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222209';

DECLARE @TenantAdminUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333301';
DECLARE @PresalesUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333302';
DECLARE @PmoUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333303';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @CandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333307';
DECLARE @ReactCandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333308';
DECLARE @AngularCandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333309';
DECLARE @HiredCandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333310';
DECLARE @HodUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333311';

DECLARE @PmoEngineeringGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444401';
DECLARE @PmoQaGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444404';
DECLARE @PmoDevOpsGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444405';
DECLARE @RecruitingDeliveryGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444402';

DECLARE @EngineeringDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';
DECLARE @QaDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02';
DECLARE @DevOpsDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03';
DECLARE @PmoDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04';
DECLARE @PresalesDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05';
DECLARE @RecruitmentDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06';

DECLARE @KarachiLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01';
DECLARE @LahoreLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02';
DECLARE @RemoteLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03';

DECLARE @AngularSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc01';
DECLARE @DotNetSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc02';
DECLARE @SqlServerSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc03';
DECLARE @AzureSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc04';
DECLARE @ReactSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc05';
DECLARE @QaAutomationSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc06';
DECLARE @DevOpsSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc07';
DECLARE @PythonSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc08';
DECLARE @AiMlSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc09';

DECLARE @LinkedInSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc001';
DECLARE @IndeedSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc002';
DECLARE @ReferralSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc003';
DECLARE @OtherSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc004';
DECLARE @JobPortalSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc005';

DECLARE @AliEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd01';
DECLARE @BilalEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd02';
DECLARE @FatimaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd03';
DECLARE @HamzaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd04';
DECLARE @AminaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd05';
DECLARE @UsmanEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd06';
DECLARE @PresalesEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd07';
DECLARE @RecruiterEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd08';
DECLARE @HodEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd09';

DECLARE @ProjectPhoenixId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01';
DECLARE @ProjectAtlasId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02';
DECLARE @ProjectReliaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03';
DECLARE @ProjectCloudOpsId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee04';
DECLARE @AssignmentBilalId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee21';
DECLARE @AssignmentFatimaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee22';
DECLARE @AssignmentHamzaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee23';
DECLARE @AssignmentAminaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee24';
DECLARE @AssignmentUsmanId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee25';

DECLARE @JobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee01';
DECLARE @HistoricalReactJobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee02';
DECLARE @HistoricalDotNetJobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee03';
DECLARE @HistoricalQaJobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee04';
DECLARE @CandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee11';
DECLARE @CandidateProspectId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee12';
DECLARE @CandidateInvitationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee13';
DECLARE @JobApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee14';
DECLARE @ReactCandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee15';
DECLARE @AngularCandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee16';
DECLARE @HiredCandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee17';
DECLARE @ReactHistoricalApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee31';
DECLARE @AngularHistoricalApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee32';
DECLARE @HiredHistoricalApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee33';
DECLARE @ReactHistoricalInterviewId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee41';
DECLARE @AngularHistoricalInterviewId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee42';
DECLARE @ReactHistoricalFeedbackId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee51';
DECLARE @AngularHistoricalFeedbackId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee52';

DECLARE @InterviewTemplateId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff01';
DECLARE @RoundScreeningId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff11';
DECLARE @RoundTechnicalId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff12';
DECLARE @RoundDepartmentHeadId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff13';
DECLARE @JobRoundScreeningId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff21';
DECLARE @JobRoundTechnicalId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff22';
DECLARE @JobRoundDepartmentHeadId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff23';

DECLARE @WorkflowDefinitionId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000001';
DECLARE @StageDraftId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000011';
DECLARE @StagePmoReviewId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000012';
DECLARE @StageSourcingId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000013';
DECLARE @StageInterviewingId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000014';
DECLARE @StageHiringManagerId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000015';
DECLARE @StageOfferId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000016';
DECLARE @StageClosedId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000017';
DECLARE @StagePresalesReviewId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000018';
DECLARE @TransitionCreateByPresalesId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000101';
DECLARE @TransitionForwardRecruiterId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000102';
DECLARE @TransitionInterviewId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000103';
DECLARE @TransitionHiringManagerId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000104';
DECLARE @TransitionRecommendEmployeesId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000105';
DECLARE @TransitionPresalesReturnPmoId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000106';
DECLARE @TransitionPresalesAcceptInternalId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000107';
DECLARE @InitialAssignmentId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000201';

MERGE dbo.TenantAiSettings AS target
USING (VALUES
    (@TenantId, N'Mock/Ollama', N'llama3.2:1b', N'nomic-embed-text', 768, N'SqlServerVector', CAST(1 AS BIT), CAST(1 AS BIT), CAST(0 AS BIT))
) AS source (TenantId, ProviderMode, LlmModel, EmbeddingModel, EmbeddingDimensions, VectorStore, ModelSwitchingLocked, HumanReviewRequired, AutoRejectEnabled)
ON target.TenantId = source.TenantId
WHEN MATCHED THEN
    UPDATE SET ProviderMode = source.ProviderMode, LlmModel = source.LlmModel, EmbeddingModel = source.EmbeddingModel, EmbeddingDimensions = source.EmbeddingDimensions,
        VectorStore = source.VectorStore, ModelSwitchingLocked = source.ModelSwitchingLocked, HumanReviewRequired = source.HumanReviewRequired,
        AutoRejectEnabled = source.AutoRejectEnabled, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (TenantId, ProviderMode, LlmModel, EmbeddingModel, EmbeddingDimensions, VectorStore, ModelSwitchingLocked, HumanReviewRequired, AutoRejectEnabled, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.TenantId, source.ProviderMode, source.LlmModel, source.EmbeddingModel, source.EmbeddingDimensions, source.VectorStore, source.ModelSwitchingLocked, source.HumanReviewRequired, source.AutoRejectEnabled, @Now, @Now);

MERGE dbo.Departments AS target
USING (VALUES
    (@EngineeringDepartmentId, @TenantId, N'ENG', N'Engineering', @HodUserId, N'Active'),
    (@QaDepartmentId, @TenantId, N'QA', N'QA', @InterviewerUserId, N'Active'),
    (@DevOpsDepartmentId, @TenantId, N'DEVOPS', N'DevOps', @InterviewerUserId, N'Active'),
    (@PmoDepartmentId, @TenantId, N'PMO', N'PMO', @PmoUserId, N'Active'),
    (@PresalesDepartmentId, @TenantId, N'PRESALES', N'Presales', @PresalesUserId, N'Active'),
    (@RecruitmentDepartmentId, @TenantId, N'RECRUITMENT', N'Recruitment', @RecruiterUserId, N'Active')
) AS source (DepartmentId, TenantId, Code, Name, LeadUserId, Status)
ON target.DepartmentId = source.DepartmentId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, LeadUserId = source.LeadUserId, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (DepartmentId, TenantId, Code, Name, LeadUserId, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.DepartmentId, source.TenantId, source.Code, source.Name, source.LeadUserId, source.Status, @Now, @Now);

MERGE dbo.Locations AS target
USING (VALUES
    (@KarachiLocationId, @TenantId, N'PK-KHI-1', N'Karachi', 'PK', N'Asia/Karachi', CAST(0 AS BIT), N'Active'),
    (@LahoreLocationId, @TenantId, N'PK-LHE-1', N'Lahore', 'PK', N'Asia/Karachi', CAST(0 AS BIT), N'Active'),
    (@RemoteLocationId, @TenantId, N'REMOTE', N'Remote', 'PK', N'Asia/Karachi', CAST(1 AS BIT), N'Active')
) AS source (LocationId, TenantId, Code, Name, CountryCode, TimezoneId, IsRemote, Status)
ON target.LocationId = source.LocationId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, CountryCode = source.CountryCode, TimezoneId = source.TimezoneId, IsRemote = source.IsRemote, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (LocationId, TenantId, Code, Name, CountryCode, TimezoneId, IsRemote, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.LocationId, source.TenantId, source.Code, source.Name, source.CountryCode, source.TimezoneId, source.IsRemote, source.Status, @Now, @Now);

MERGE dbo.Skills AS target
USING (VALUES
    (@AngularSkillId, @TenantId, N'Angular', N'angular', N'Frontend', N'["Angular 17","Angular 18"]'),
    (@DotNetSkillId, @TenantId, N'.NET', N'.net', N'Backend', N'["ASP.NET Core","C#"]'),
    (@SqlServerSkillId, @TenantId, N'SQL Server', N'sql server', N'Database', N'["T-SQL","Microsoft SQL Server"]'),
    (@AzureSkillId, @TenantId, N'Azure', N'azure', N'Cloud', N'["Azure App Service","Azure SQL"]'),
    (@ReactSkillId, @TenantId, N'React', N'react', N'Frontend', N'["React.js"]'),
    (@QaAutomationSkillId, @TenantId, N'QA Automation', N'qa automation', N'Quality', N'["Selenium","Playwright"]'),
    (@DevOpsSkillId, @TenantId, N'DevOps', N'devops', N'Platform', N'["CI/CD","Docker"]'),
    (@PythonSkillId, @TenantId, N'Python', N'python', N'Backend', N'["FastAPI"]'),
    (@AiMlSkillId, @TenantId, N'AI/ML', N'ai/ml', N'Data', N'["LLM","Embeddings"]')
) AS source (SkillId, TenantId, Name, NormalizedName, Category, AliasesJson)
ON target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET Name = source.Name, NormalizedName = source.NormalizedName, Category = source.Category, AliasesJson = source.AliasesJson, Status = N'Active', UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (SkillId, TenantId, Name, NormalizedName, Category, AliasesJson, IsVectorRelevant, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.SkillId, source.TenantId, source.Name, source.NormalizedName, source.Category, source.AliasesJson, 1, N'Active', @Now, @Now);

MERGE dbo.CandidateSourceLabels AS target
USING (VALUES
    (@LinkedInSourceLabelId, @TenantId, N'LinkedInManual', N'LinkedIn', N'External sourcing', N'Active'),
    (@IndeedSourceLabelId, @TenantId, N'IndeedManual', N'Indeed', N'External sourcing', N'Active'),
    (@ReferralSourceLabelId, @TenantId, N'Referral', N'Referral', N'Referral reporting', N'Active'),
    (@OtherSourceLabelId, @TenantId, N'Other', N'Other', N'Manual review', N'Active'),
    (@JobPortalSourceLabelId, @TenantId, N'JobPortal', N'Job Portal', N'Talent Pilot portal', N'Active')
) AS source (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status)
ON target.TenantId = source.TenantId AND target.Code = source.Code
WHEN MATCHED THEN UPDATE SET Code = source.Code, DisplayName = source.DisplayName, ReportingCategory = source.ReportingCategory, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.CandidateSourceLabelId, source.TenantId, source.Code, source.DisplayName, source.ReportingCategory, source.Status, @Now, @Now);

MERGE dbo.Projects AS target
USING (VALUES
    (@ProjectPhoenixId, @TenantId, @EngineeringDepartmentId, N'PHX', N'Phoenix Delivery Platform', N'Confidential Client', N'Active', CONVERT(date, '2026-01-01'), NULL),
    (@ProjectAtlasId, @TenantId, @EngineeringDepartmentId, N'ATL', N'Atlas Modernization', N'Enterprise Client', N'Active', CONVERT(date, '2026-02-01'), NULL),
    (@ProjectReliaId, @TenantId, @EngineeringDepartmentId, N'REL', N'Relia Operations Portal', N'Relia', N'Closed', CONVERT(date, '2024-01-15'), CONVERT(date, '2024-12-20')),
    (@ProjectCloudOpsId, @TenantId, @DevOpsDepartmentId, N'CLO', N'CloudOps Automation Platform', N'Enterprise Client', N'Closed', CONVERT(date, '2023-04-01'), CONVERT(date, '2023-11-30'))
) AS source (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn)
ON target.ProjectId = source.ProjectId
WHEN MATCHED THEN UPDATE SET DepartmentId = source.DepartmentId, Code = source.Code, Name = source.Name, ClientName = source.ClientName, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.ProjectId, source.TenantId, source.DepartmentId, source.Code, source.Name, source.ClientName, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);

MERGE dbo.Employees AS target
USING (VALUES
    (@AliEmployeeId, @TenantId, @PmoUserId, N'TKX-1001', N'EXT-1001', N'Ali Khan', N'ai-pmo@8pkk57.onmicrosoft.com', @PmoDepartmentId, @KarachiLocationId, N'PMO Manager', CAST(8.0 AS DECIMAL(4,1)), CONVERT(date, '2018-06-04'), N'Allocated', N'Allocated', N'Active'),
    (@BilalEmployeeId, @TenantId, @InterviewerUserId, N'TKX-1002', N'EXT-1002', N'Bilal Hussain', N'ai-interviewer@8pkk57.onmicrosoft.com', @EngineeringDepartmentId, @KarachiLocationId, N'Senior Software Engineer', CAST(6.0 AS DECIMAL(4,1)), CONVERT(date, '2020-04-20'), N'Allocated', N'Allocated', N'Active'),
    (@FatimaEmployeeId, @TenantId, @HiringManagerUserId, N'TKX-1003', N'EXT-1003', N'Fatima Noor', N'ai-hiring.manager@8pkk57.onmicrosoft.com', @EngineeringDepartmentId, @LahoreLocationId, N'Engineering Manager', CAST(10.0 AS DECIMAL(4,1)), CONVERT(date, '2016-09-05'), N'Allocated', N'Allocated', N'Active'),
    (@HodEmployeeId, @TenantId, @HodUserId, N'TKX-1009', N'EXT-1009', N'Zara Siddiqui', N'ai-hod.engineering@8pkk57.onmicrosoft.com', @EngineeringDepartmentId, @LahoreLocationId, N'Head of Engineering', CAST(13.0 AS DECIMAL(4,1)), CONVERT(date, '2014-02-10'), N'Allocated', N'Allocated', N'Active'),
    (@HamzaEmployeeId, @TenantId, NULL, N'TKX-1004', N'EXT-1004', N'Hamza Ali', N'hamza.ali@tkxel.com', @EngineeringDepartmentId, @KarachiLocationId, N'Senior .NET Engineer', CAST(5.5 AS DECIMAL(4,1)), CONVERT(date, '2021-08-02'), N'Available', N'Benched', N'Active'),
    (@AminaEmployeeId, @TenantId, NULL, N'TKX-1005', N'EXT-1005', N'Amina Shah', N'amina.shah@tkxel.com', @EngineeringDepartmentId, @RemoteLocationId, N'Angular Engineer', CAST(4.0 AS DECIMAL(4,1)), CONVERT(date, '2022-05-09'), N'Available', N'Benched', N'Active'),
    (@UsmanEmployeeId, @TenantId, NULL, N'TKX-1006', N'EXT-1006', N'Usman Tariq', N'usman.tariq@tkxel.com', @DevOpsDepartmentId, @LahoreLocationId, N'DevOps Engineer', CAST(5.0 AS DECIMAL(4,1)), CONVERT(date, '2021-11-15'), N'Available', N'Benched', N'Active'),
    (@PresalesEmployeeId, @TenantId, @PresalesUserId, N'TKX-1007', N'EXT-1007', N'Ahmed Raza', N'ai-presales@8pkk57.onmicrosoft.com', @PresalesDepartmentId, @KarachiLocationId, N'Presales Consultant', CAST(7.0 AS DECIMAL(4,1)), CONVERT(date, '2019-03-11'), N'Allocated', N'Allocated', N'Active'),
    (@RecruiterEmployeeId, @TenantId, @RecruiterUserId, N'TKX-1008', N'EXT-1008', N'Sara Malik', N'ai-recruiter@8pkk57.onmicrosoft.com', @RecruitmentDepartmentId, @LahoreLocationId, N'Talent Acquisition Specialist', CAST(5.0 AS DECIMAL(4,1)), CONVERT(date, '2021-01-18'), N'Allocated', N'Allocated', N'Active')
) AS source (EmployeeId, TenantId, AppUserId, EmployeeCode, ExternalEmployeeId, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, JoiningDate, AvailabilityStatus, BenchStatus, Status)
ON target.EmployeeId = source.EmployeeId
WHEN MATCHED THEN UPDATE SET AppUserId = source.AppUserId, EmployeeCode = source.EmployeeCode, ExternalEmployeeId = source.ExternalEmployeeId, DisplayName = source.DisplayName,
    Email = source.Email, DepartmentId = source.DepartmentId, LocationId = source.LocationId, Designation = source.Designation, ExperienceYears = source.ExperienceYears,
    JoiningDate = source.JoiningDate, AvailabilityStatus = source.AvailabilityStatus, BenchStatus = source.BenchStatus, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (EmployeeId, TenantId, AppUserId, EmployeeCode, ExternalEmployeeId, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, JoiningDate, AvailabilityStatus, BenchStatus, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.EmployeeId, source.TenantId, source.AppUserId, source.EmployeeCode, source.ExternalEmployeeId, source.DisplayName, source.Email, source.DepartmentId, source.LocationId, source.Designation, source.ExperienceYears, source.JoiningDate, source.AvailabilityStatus, source.BenchStatus, source.Status, @Now, @Now);

MERGE dbo.EmployeeProjectAssignments AS target
USING (VALUES
    (@AssignmentBilalId, @TenantId, @BilalEmployeeId, @ProjectPhoenixId, 100, N'Active', CONVERT(date, '2026-01-01'), NULL),
    (@AssignmentFatimaId, @TenantId, @FatimaEmployeeId, @ProjectAtlasId, 100, N'Active', CONVERT(date, '2026-02-01'), NULL),
    (@AssignmentHamzaId, @TenantId, @HamzaEmployeeId, @ProjectReliaId, 100, N'Completed', CONVERT(date, '2024-01-15'), CONVERT(date, '2024-12-20')),
    (@AssignmentAminaId, @TenantId, @AminaEmployeeId, @ProjectReliaId, 75, N'Completed', CONVERT(date, '2024-03-01'), CONVERT(date, '2024-10-31')),
    (@AssignmentUsmanId, @TenantId, @UsmanEmployeeId, @ProjectCloudOpsId, 100, N'Completed', CONVERT(date, '2023-04-01'), CONVERT(date, '2023-11-30'))
) AS source (ProjectAssignmentId, TenantId, EmployeeId, ProjectId, AllocationPercent, Status, StartsOn, EndsOn)
ON target.ProjectAssignmentId = source.ProjectAssignmentId
WHEN MATCHED THEN UPDATE SET AllocationPercent = source.AllocationPercent, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (ProjectAssignmentId, TenantId, EmployeeId, ProjectId, AllocationPercent, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.ProjectAssignmentId, source.TenantId, source.EmployeeId, source.ProjectId, source.AllocationPercent, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);

MERGE dbo.EmployeeSkills AS target
USING (VALUES
    (@TenantId, @HamzaEmployeeId, @DotNetSkillId, N'Advanced', CAST(5.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @HamzaEmployeeId, @SqlServerSkillId, N'Advanced', CAST(4.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @AminaEmployeeId, @AngularSkillId, N'Advanced', CAST(4.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @AminaEmployeeId, @ReactSkillId, N'Intermediate', CAST(2.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @UsmanEmployeeId, @DevOpsSkillId, N'Advanced', CAST(5.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @BilalEmployeeId, @DotNetSkillId, N'Advanced', CAST(6.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @BilalEmployeeId, @AzureSkillId, N'Intermediate', CAST(3.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @HodEmployeeId, @ReactSkillId, N'Advanced', CAST(8.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @HodEmployeeId, @DotNetSkillId, N'Advanced', CAST(8.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @HodEmployeeId, @AzureSkillId, N'Advanced', CAST(6.0 AS DECIMAL(4,1)), CAST(0 AS BIT))
) AS source (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary)
ON target.TenantId = source.TenantId AND target.EmployeeId = source.EmployeeId AND target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
WHEN NOT MATCHED THEN INSERT (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
VALUES (source.TenantId, source.EmployeeId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary, @Now);

MERGE dbo.AppUsers AS target
USING (VALUES
    (@ReactCandidateUserId, @TenantId, N'Nida Farooq', N'nida.farooq@example.com', N'nida.farooq@example.com', N'NF', N'Active'),
    (@AngularCandidateUserId, @TenantId, N'Omar Sheikh', N'omar.sheikh@example.com', N'omar.sheikh@example.com', N'OS', N'Active'),
    (@HiredCandidateUserId, @TenantId, N'Zara Iqbal', N'zara.iqbal@example.com', N'zara.iqbal@example.com', N'ZI', N'Active')
) AS source (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus)
ON target.UserId = source.UserId
WHEN MATCHED THEN UPDATE SET
    TenantId = source.TenantId,
    DisplayName = source.DisplayName,
    Email = source.Email,
    EmailNormalized = UPPER(source.EmailNormalized),
    Initials = source.Initials,
    AccountStatus = source.AccountStatus,
    UpdatedAtUtc = @Now,
    DeletedAtUtc = NULL
WHEN NOT MATCHED THEN INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.UserId, source.TenantId, source.DisplayName, source.Email, UPPER(source.EmailNormalized), source.Initials, source.AccountStatus, @Now, @Now);

MERGE dbo.UserCredentials AS target
USING (VALUES
    ('77777777-7777-7777-7777-777777777308', @TenantId, @ReactCandidateUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777309', @TenantId, @AngularCandidateUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777310', @TenantId, @HiredCandidateUserId, @DemoPasswordHash)
) AS source (UserCredentialId, TenantId, UserId, PasswordHash)
ON target.UserId = source.UserId
WHEN MATCHED THEN UPDATE SET
    TenantId = source.TenantId,
    PasswordHash = source.PasswordHash,
    PasswordUpdatedAtUtc = @Now,
    UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (UserCredentialId, TenantId, UserId, PasswordHash, PasswordUpdatedAtUtc, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.UserCredentialId, source.TenantId, source.UserId, source.PasswordHash, @Now, @Now, @Now);

MERGE dbo.Candidates AS target
USING (VALUES
    (@CandidateId, @TenantId, @CandidateUserId, N'Ayesha Khan', N'ai-candidate@8pkk57.onmicrosoft.com', N'+92-300-0000000', N'https://linkedin.com/in/ayesha-khan', N'Senior Software Engineer', N'Previous Employer', CAST(5.0 AS DECIMAL(4,1)), CAST(450000 AS DECIMAL(18,2)), 'PKR', 30, N'Active'),
    (@ReactCandidateId, @TenantId, @ReactCandidateUserId, N'Nida Farooq', N'nida.farooq@example.com', N'+92-300-0000001', N'https://linkedin.com/in/nida-farooq', N'Senior React Developer', N'Product Studio', CAST(6.5 AS DECIMAL(4,1)), CAST(520000 AS DECIMAL(18,2)), 'PKR', 15, N'Active'),
    (@AngularCandidateId, @TenantId, @AngularCandidateUserId, N'Omar Sheikh', N'omar.sheikh@example.com', N'+92-300-0000002', N'https://linkedin.com/in/omar-sheikh', N'Frontend Engineer', N'Consulting Partner', CAST(4.5 AS DECIMAL(4,1)), CAST(390000 AS DECIMAL(18,2)), 'PKR', 30, N'Active'),
    (@HiredCandidateId, @TenantId, @HiredCandidateUserId, N'Zara Iqbal', N'zara.iqbal@example.com', N'+92-300-0000003', N'https://linkedin.com/in/zara-iqbal', N'QA Automation Engineer', N'Quality Guild', CAST(5.5 AS DECIMAL(4,1)), CAST(360000 AS DECIMAL(18,2)), 'PKR', 0, N'Hired')
) AS source (CandidateId, TenantId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, ExpectedSalaryAmount, ExpectedSalaryCurrency, NoticePeriodDays, Status)
ON target.CandidateId = source.CandidateId
WHEN MATCHED THEN UPDATE SET DisplayName = source.DisplayName, Email = source.Email, Phone = source.Phone, LinkedInUrl = source.LinkedInUrl, CurrentDesignation = source.CurrentDesignation,
    CurrentCompany = source.CurrentCompany, ExperienceYears = source.ExperienceYears, ExpectedSalaryAmount = source.ExpectedSalaryAmount, ExpectedSalaryCurrency = source.ExpectedSalaryCurrency,
    NoticePeriodDays = source.NoticePeriodDays, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (CandidateId, TenantId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, ExpectedSalaryAmount, ExpectedSalaryCurrency, NoticePeriodDays, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.CandidateId, source.TenantId, source.AppUserId, source.DisplayName, source.Email, source.Phone, source.LinkedInUrl, source.CurrentDesignation, source.CurrentCompany, source.ExperienceYears, source.ExpectedSalaryAmount, source.ExpectedSalaryCurrency, source.NoticePeriodDays, source.Status, @Now, @Now);

MERGE dbo.CandidateSkills AS target
USING (VALUES
    (@TenantId, @CandidateId, @DotNetSkillId, N'Advanced', CAST(5.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @CandidateId, @SqlServerSkillId, N'Intermediate', CAST(3.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @CandidateId, @AngularSkillId, N'Intermediate', CAST(2.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @ReactCandidateId, @ReactSkillId, N'Advanced', CAST(6.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @ReactCandidateId, @AngularSkillId, N'Advanced', CAST(4.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @ReactCandidateId, @AzureSkillId, N'Intermediate', CAST(2.5 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @ReactCandidateId, @SqlServerSkillId, N'Intermediate', CAST(2.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @AngularCandidateId, @AngularSkillId, N'Advanced', CAST(4.5 AS DECIMAL(4,1)), CAST(1 AS BIT)),
    (@TenantId, @AngularCandidateId, @ReactSkillId, N'Intermediate', CAST(2.5 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @AngularCandidateId, @DotNetSkillId, N'Beginner', CAST(1.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
    (@TenantId, @HiredCandidateId, @QaAutomationSkillId, N'Advanced', CAST(5.0 AS DECIMAL(4,1)), CAST(1 AS BIT))
) AS source (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary)
ON target.TenantId = source.TenantId AND target.CandidateId = source.CandidateId AND target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
WHEN NOT MATCHED THEN INSERT (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
VALUES (source.TenantId, source.CandidateId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary, @Now);

MERGE dbo.JobRequests AS target
USING (VALUES
    (@JobRequestId, @TenantId, N'TP-REQ-0001', N'Senior .NET Engineer', N'Client needs a senior .NET engineer with SQL Server, Azure, and Angular exposure.', N'Enterprise Client', @EngineeringDepartmentId, @KarachiLocationId, N'FullTime', CAST(5.0 AS DECIMAL(4,1)), CAST(8.0 AS DECIMAL(4,1)), N'High', 1, 0, N'PMOReview', N'Published', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @PresalesUserId, N'PMO_REVIEW', DATEADD(DAY, -1, @Now)),
    (@HistoricalReactJobRequestId, @TenantId, N'TP-HIST-0001', N'React Portal Engineer', N'Historical frontend role focused on React, Angular migration support, Azure-hosted customer portals, and SQL-backed reporting pages.', N'Relia', @EngineeringDepartmentId, @LahoreLocationId, N'FullTime', CAST(4.0 AS DECIMAL(4,1)), CAST(7.0 AS DECIMAL(4,1)), N'Medium', 1, 1, N'Closed', N'Unpublished', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @RecruiterUserId, N'CLOSED', DATEADD(DAY, -180, @Now)),
    (@HistoricalDotNetJobRequestId, @TenantId, N'TP-HIST-0002', N'Angular .NET Product Engineer', N'Historical full-stack role requiring Angular, React familiarity, .NET API collaboration, SQL Server debugging, and delivery with product teams.', N'Enterprise Client', @EngineeringDepartmentId, @RemoteLocationId, N'FullTime', CAST(3.0 AS DECIMAL(4,1)), CAST(6.0 AS DECIMAL(4,1)), N'Medium', 1, 0, N'Closed', N'Unpublished', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @RecruiterUserId, N'CLOSED', DATEADD(DAY, -120, @Now)),
    (@HistoricalQaJobRequestId, @TenantId, N'TP-HIST-0003', N'QA Automation Engineer', N'Historical quality engineering role focused on regression automation, API test coverage, and release readiness.', N'Internal Platform', @QaDepartmentId, @KarachiLocationId, N'FullTime', CAST(4.0 AS DECIMAL(4,1)), CAST(7.0 AS DECIMAL(4,1)), N'Low', 1, 1, N'Closed', N'Unpublished', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @RecruiterUserId, N'CLOSED', DATEADD(DAY, -90, @Now))
) AS source (JobRequestId, TenantId, RequestCode, Title, Description, ClientName, DepartmentId, LocationId, EmploymentType, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, Status, PublishStatus, HiringManagerUserId, HiringManagerGroupId, CreatedByUserId, CurrentStageKey, PublishedAtUtc)
ON target.JobRequestId = source.JobRequestId
WHEN MATCHED THEN UPDATE SET Title = source.Title, Description = source.Description, ClientName = source.ClientName, DepartmentId = source.DepartmentId, LocationId = source.LocationId,
    EmploymentType = source.EmploymentType, ExperienceMinYears = source.ExperienceMinYears, ExperienceMaxYears = source.ExperienceMaxYears, Priority = source.Priority,
    RequiredPositions = source.RequiredPositions, FulfilledPositions = source.FulfilledPositions, Status = source.Status, PublishStatus = source.PublishStatus,
    HiringManagerUserId = source.HiringManagerUserId, HiringManagerGroupId = source.HiringManagerGroupId, CreatedByUserId = source.CreatedByUserId,
    CurrentStageKey = source.CurrentStageKey, PublishedAtUtc = source.PublishedAtUtc, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (JobRequestId, TenantId, RequestCode, Title, Description, ClientName, DepartmentId, LocationId, EmploymentType, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, Status, PublishStatus, HiringManagerUserId, HiringManagerGroupId, CreatedByUserId, CurrentStageKey, PublishedAtUtc, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.JobRequestId, source.TenantId, source.RequestCode, source.Title, source.Description, source.ClientName, source.DepartmentId, source.LocationId, source.EmploymentType, source.ExperienceMinYears, source.ExperienceMaxYears, source.Priority, source.RequiredPositions, source.FulfilledPositions, source.Status, source.PublishStatus, source.HiringManagerUserId, source.HiringManagerGroupId, source.CreatedByUserId, source.CurrentStageKey, source.PublishedAtUtc, @Now, @Now);

MERGE dbo.JobRequestSkills AS target
USING (VALUES
    (@TenantId, @JobRequestId, @DotNetSkillId, CAST(1 AS BIT), 10),
    (@TenantId, @JobRequestId, @SqlServerSkillId, CAST(1 AS BIT), 8),
    (@TenantId, @JobRequestId, @AzureSkillId, CAST(1 AS BIT), 6),
    (@TenantId, @JobRequestId, @AngularSkillId, CAST(0 AS BIT), 5),
    (@TenantId, @HistoricalReactJobRequestId, @ReactSkillId, CAST(1 AS BIT), 10),
    (@TenantId, @HistoricalReactJobRequestId, @AngularSkillId, CAST(0 AS BIT), 7),
    (@TenantId, @HistoricalReactJobRequestId, @AzureSkillId, CAST(0 AS BIT), 5),
    (@TenantId, @HistoricalDotNetJobRequestId, @AngularSkillId, CAST(1 AS BIT), 9),
    (@TenantId, @HistoricalDotNetJobRequestId, @ReactSkillId, CAST(0 AS BIT), 6),
    (@TenantId, @HistoricalDotNetJobRequestId, @DotNetSkillId, CAST(0 AS BIT), 5),
    (@TenantId, @HistoricalDotNetJobRequestId, @SqlServerSkillId, CAST(0 AS BIT), 4),
    (@TenantId, @HistoricalQaJobRequestId, @QaAutomationSkillId, CAST(1 AS BIT), 10)
) AS source (TenantId, JobRequestId, SkillId, IsRequired, Weight)
ON target.TenantId = source.TenantId AND target.JobRequestId = source.JobRequestId AND target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET IsRequired = source.IsRequired, Weight = source.Weight
WHEN NOT MATCHED THEN INSERT (TenantId, JobRequestId, SkillId, IsRequired, Weight, CreatedAtUtc)
VALUES (source.TenantId, source.JobRequestId, source.SkillId, source.IsRequired, source.Weight, @Now);

MERGE dbo.CandidateProspects AS target
USING (VALUES
    (@CandidateProspectId, @TenantId, N'Ayesha Khan', N'ai-candidate@8pkk57.onmicrosoft.com', N'+92-300-0000000', N'https://linkedin.com/in/ayesha-khan', @LinkedInSourceLabelId, N'LinkedIn', N'Registered', @CandidateId, @RecruiterUserId)
) AS source (CandidateProspectId, TenantId, DisplayName, Email, Phone, LinkedInUrl, CandidateSourceLabelId, SourceLabel, Status, CandidateId, CreatedByUserId)
ON target.CandidateProspectId = source.CandidateProspectId
WHEN MATCHED THEN UPDATE SET CandidateSourceLabelId = source.CandidateSourceLabelId, SourceLabel = source.SourceLabel, Status = source.Status, CandidateId = source.CandidateId, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (CandidateProspectId, TenantId, DisplayName, Email, Phone, LinkedInUrl, CandidateSourceLabelId, SourceLabel, Status, CandidateId, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.CandidateProspectId, source.TenantId, source.DisplayName, source.Email, source.Phone, source.LinkedInUrl, source.CandidateSourceLabelId, source.SourceLabel, source.Status, source.CandidateId, source.CreatedByUserId, @Now, @Now);

MERGE dbo.CandidateProspectJobRequests AS target
USING (VALUES (@TenantId, @CandidateProspectId, @JobRequestId, N'Applied', N'Rediscovered similar .NET candidate.'))
AS source (TenantId, CandidateProspectId, JobRequestId, Status, Notes)
ON target.TenantId = source.TenantId AND target.CandidateProspectId = source.CandidateProspectId AND target.JobRequestId = source.JobRequestId
WHEN MATCHED THEN UPDATE SET Status = source.Status, Notes = source.Notes
WHEN NOT MATCHED THEN INSERT (TenantId, CandidateProspectId, JobRequestId, Status, Notes, CreatedAtUtc)
VALUES (source.TenantId, source.CandidateProspectId, source.JobRequestId, source.Status, source.Notes, @Now);

DELETE feedback
FROM dbo.InterviewFeedback AS feedback
INNER JOIN dbo.Interviews AS interview ON interview.InterviewId = feedback.InterviewId
WHERE interview.JobApplicationId = @JobApplicationId;

DELETE participant
FROM dbo.InterviewParticipants AS participant
INNER JOIN dbo.Interviews AS interview ON interview.InterviewId = participant.InterviewId
WHERE interview.JobApplicationId = @JobApplicationId;

DELETE FROM dbo.Interviews WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.JobApplicationDocuments WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.JobApplicationStatusHistory WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.JobRequestFulfillments WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.OfferPresentationMeetings WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.OfferLetters WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.AiRecommendationLogs WHERE RecommendedEntityId = @JobApplicationId OR SourceEntityId = @JobApplicationId;
DELETE FROM dbo.AiAgentRuns WHERE SourceEntityId = @JobApplicationId;
DELETE FROM dbo.VectorEmbeddings WHERE EntityId = @JobApplicationId;
DELETE FROM dbo.JobApplications WHERE JobApplicationId = @JobApplicationId;
DELETE FROM dbo.CandidateInvitations WHERE CandidateInvitationId = @CandidateInvitationId;

MERGE dbo.JobApplications AS target
USING (VALUES
    (@ReactHistoricalApplicationId, @TenantId, @HistoricalReactJobRequestId, @ReactCandidateId, @ReferralSourceLabelId, N'Referral', N'Rejected', 1, CAST(0 AS BIT), CAST(0 AS BIT), DATEADD(DAY, -170, @Now), DATEADD(DAY, -170, @Now), DATEADD(DAY, -150, @Now), N'Client selected a local full-stack profile; interviewer feedback stayed positive.'),
    (@AngularHistoricalApplicationId, @TenantId, @HistoricalDotNetJobRequestId, @AngularCandidateId, @LinkedInSourceLabelId, N'LinkedIn', N'OfferDeclined', 1, CAST(0 AS BIT), CAST(0 AS BIT), DATEADD(DAY, -115, @Now), DATEADD(DAY, -115, @Now), DATEADD(DAY, -95, @Now), N'Offer declined due to notice period and compensation timing; feedback recommended keeping warm.'),
    (@HiredHistoricalApplicationId, @TenantId, @HistoricalQaJobRequestId, @HiredCandidateId, @ReferralSourceLabelId, N'Referral', N'Hired', 1, CAST(0 AS BIT), CAST(0 AS BIT), DATEADD(DAY, -85, @Now), DATEADD(DAY, -85, @Now), DATEADD(DAY, -60, @Now), N'Hired on a historical QA role and excluded from rediscovery.')
) AS source (JobApplicationId, TenantId, JobRequestId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason)
ON target.JobApplicationId = source.JobApplicationId
WHEN MATCHED THEN UPDATE SET CandidateSourceLabelId = source.CandidateSourceLabelId, SourceLabel = source.SourceLabel, CurrentStatus = source.CurrentStatus, IsActive = source.IsActive, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (JobApplicationId, TenantId, JobRequestId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.JobApplicationId, source.TenantId, source.JobRequestId, source.CandidateId, source.CandidateSourceLabelId, source.SourceLabel, source.CurrentStatus, source.ApplicationVersion, source.IsActive, source.IsInvited, source.ConfirmedAtUtc, source.AppliedAtUtc, source.FinalDecisionAtUtc, source.FinalDecisionReason, @Now, @Now);

MERGE dbo.Interviews AS target
USING (VALUES
    (@ReactHistoricalInterviewId, @TenantId, @ReactHistoricalApplicationId, CAST(NULL AS UNIQUEIDENTIFIER), @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -160, @Now), 60, N'Historical technical interview', CAST(NULL AS NVARCHAR(300)), N'Completed'),
    (@AngularHistoricalInterviewId, @TenantId, @AngularHistoricalApplicationId, CAST(NULL AS UNIQUEIDENTIFIER), @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -105, @Now), 60, N'Historical technical interview', CAST(NULL AS NVARCHAR(300)), N'Completed')
) AS source (InterviewId, TenantId, JobApplicationId, JobRequestInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, MeetingLink, LocationText, Status)
ON target.InterviewId = source.InterviewId
WHEN MATCHED THEN UPDATE SET
    JobApplicationId = source.JobApplicationId,
    InterviewerUserId = source.InterviewerUserId,
    ScheduledByUserId = source.ScheduledByUserId,
    StartsAtUtc = source.StartsAtUtc,
    DurationMinutes = source.DurationMinutes,
    MeetingLink = source.MeetingLink,
    LocationText = source.LocationText,
    Status = source.Status,
    UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (InterviewId, TenantId, JobApplicationId, JobRequestInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, MeetingLink, LocationText, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.InterviewId, source.TenantId, source.JobApplicationId, source.JobRequestInterviewRoundId, source.InterviewerUserId, source.ScheduledByUserId, source.StartsAtUtc, source.DurationMinutes, source.MeetingLink, source.LocationText, source.Status, @Now, @Now);

MERGE dbo.InterviewFeedback AS target
USING (VALUES
    (@ReactHistoricalFeedbackId, @TenantId, @ReactHistoricalInterviewId, @InterviewerUserId, 4, 4, 5, N'Proceed', N'Strong React and portal delivery experience. Good Azure exposure and clear communication; would need a short ramp-up on backend depth.', CAST(1 AS BIT), DATEADD(DAY, -159, @Now)),
    (@AngularHistoricalFeedbackId, @TenantId, @AngularHistoricalInterviewId, @InterviewerUserId, 4, 3, 4, N'Proceed', N'Good Angular product delivery and can support React tasks. Needs validation on SQL Server depth, but previous interviewers marked the candidate as a warm future-fit profile.', CAST(1 AS BIT), DATEADD(DAY, -104, @Now))
) AS source (InterviewFeedbackId, TenantId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText, IsSubmitted, SubmittedAtUtc)
ON target.InterviewFeedbackId = source.InterviewFeedbackId
WHEN MATCHED THEN UPDATE SET
    SubmittedByUserId = source.SubmittedByUserId,
    TechnicalScore = source.TechnicalScore,
    CommunicationScore = source.CommunicationScore,
    CultureScore = source.CultureScore,
    Recommendation = source.Recommendation,
    FeedbackText = source.FeedbackText,
    IsSubmitted = source.IsSubmitted,
    SubmittedAtUtc = source.SubmittedAtUtc,
    UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (InterviewFeedbackId, TenantId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText, IsSubmitted, SubmittedAtUtc, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.InterviewFeedbackId, source.TenantId, source.InterviewId, source.SubmittedByUserId, source.TechnicalScore, source.CommunicationScore, source.CultureScore, source.Recommendation, source.FeedbackText, source.IsSubmitted, source.SubmittedAtUtc, @Now, @Now);

MERGE dbo.WorkflowDefinitions AS target
USING (VALUES (@WorkflowDefinitionId, @TenantId, N'JOB_REQUEST_MVP', N'Job Request MVP Workflow', N'JobRequest', N'Active'))
AS source (WorkflowDefinitionId, TenantId, Code, Name, EntityType, Status)
ON target.WorkflowDefinitionId = source.WorkflowDefinitionId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, EntityType = source.EntityType, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (WorkflowDefinitionId, TenantId, Code, Name, EntityType, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.WorkflowDefinitionId, source.TenantId, source.Code, source.Name, source.EntityType, source.Status, @Now, @Now);

MERGE dbo.WorkflowStages AS target
USING (VALUES
    (@StageDraftId, @TenantId, @WorkflowDefinitionId, N'DRAFT', N'Draft', 10, CAST(0 AS BIT), N'Active'),
    (@StagePmoReviewId, @TenantId, @WorkflowDefinitionId, N'PMO_REVIEW', N'PMO Review', 20, CAST(0 AS BIT), N'Active'),
    (@StagePresalesReviewId, @TenantId, @WorkflowDefinitionId, N'PRESALES_REVIEW', N'Presales Review', 25, CAST(0 AS BIT), N'Active'),
    (@StageSourcingId, @TenantId, @WorkflowDefinitionId, N'SOURCING', N'Recruiter Sourcing', 30, CAST(0 AS BIT), N'Active'),
    (@StageInterviewingId, @TenantId, @WorkflowDefinitionId, N'INTERVIEWING', N'Interviewing', 40, CAST(0 AS BIT), N'Active'),
    (@StageHiringManagerId, @TenantId, @WorkflowDefinitionId, N'HIRING_MANAGER_REVIEW', N'Hiring Manager Review', 50, CAST(0 AS BIT), N'Active'),
    (@StageOfferId, @TenantId, @WorkflowDefinitionId, N'OFFER', N'Offer', 60, CAST(0 AS BIT), N'Active'),
    (@StageClosedId, @TenantId, @WorkflowDefinitionId, N'CLOSED', N'Closed', 70, CAST(1 AS BIT), N'Active')
) AS source (WorkflowStageId, TenantId, WorkflowDefinitionId, StageKey, Name, StageOrder, IsTerminal, Status)
ON target.WorkflowDefinitionId = source.WorkflowDefinitionId
   AND target.StageKey = source.StageKey
WHEN MATCHED THEN UPDATE SET StageKey = source.StageKey, Name = source.Name, StageOrder = source.StageOrder, IsTerminal = source.IsTerminal, Status = source.Status
WHEN NOT MATCHED THEN INSERT (WorkflowStageId, TenantId, WorkflowDefinitionId, StageKey, Name, StageOrder, IsTerminal, Status)
VALUES (source.WorkflowStageId, source.TenantId, source.WorkflowDefinitionId, source.StageKey, source.Name, source.StageOrder, source.IsTerminal, source.Status);

MERGE dbo.WorkflowTransitions AS target
USING
(
    SELECT
        source.WorkflowTransitionId,
        source.TenantId,
        source.WorkflowDefinitionId,
        source.ActionKey,
        source.Name,
        fromStage.WorkflowStageId AS FromStageId,
        toStage.WorkflowStageId AS ToStageId,
        source.Status
    FROM
    (
        VALUES
            (@TransitionCreateByPresalesId, @TenantId, @WorkflowDefinitionId, N'CREATE_BY_PRESALES', N'Create by Presales', N'DRAFT', N'PMO_REVIEW', N'Active'),
            (@TransitionRecommendEmployeesId, @TenantId, @WorkflowDefinitionId, N'RECOMMEND_EMPLOYEES_TO_PRESALES', N'Recommend Employees to Presales', N'PMO_REVIEW', N'PRESALES_REVIEW', N'Active'),
            (@TransitionPresalesReturnPmoId, @TenantId, @WorkflowDefinitionId, N'PRESALES_RETURN_TO_PMO', N'Presales Return to PMO', N'PRESALES_REVIEW', N'PMO_REVIEW', N'Active'),
            (@TransitionPresalesAcceptInternalId, @TenantId, @WorkflowDefinitionId, N'PRESALES_ACCEPT_INTERNAL_EMPLOYEE', N'Presales Accept Internal Employee', N'PRESALES_REVIEW', N'CLOSED', N'Active'),
            (@TransitionForwardRecruiterId, @TenantId, @WorkflowDefinitionId, N'FORWARD_TO_RECRUITER', N'Forward to Recruiter', N'PMO_REVIEW', N'SOURCING', N'Active'),
            (@TransitionInterviewId, @TenantId, @WorkflowDefinitionId, N'MOVE_TO_INTERVIEWING', N'Move to Interviewing', N'SOURCING', N'INTERVIEWING', N'Active'),
            (@TransitionHiringManagerId, @TenantId, @WorkflowDefinitionId, N'FORWARD_TO_HIRING_MANAGER', N'Forward to Hiring Manager', N'INTERVIEWING', N'HIRING_MANAGER_REVIEW', N'Active')
    ) AS source (WorkflowTransitionId, TenantId, WorkflowDefinitionId, ActionKey, Name, FromStageKey, ToStageKey, Status)
    INNER JOIN dbo.WorkflowStages AS fromStage
        ON fromStage.WorkflowDefinitionId = source.WorkflowDefinitionId
        AND fromStage.StageKey = source.FromStageKey
    INNER JOIN dbo.WorkflowStages AS toStage
        ON toStage.WorkflowDefinitionId = source.WorkflowDefinitionId
        AND toStage.StageKey = source.ToStageKey
) AS source
ON target.WorkflowDefinitionId = source.WorkflowDefinitionId
   AND target.ActionKey = source.ActionKey
WHEN MATCHED THEN UPDATE SET ActionKey = source.ActionKey, Name = source.Name, FromStageId = source.FromStageId, ToStageId = source.ToStageId, Status = source.Status
WHEN NOT MATCHED THEN INSERT (WorkflowTransitionId, TenantId, WorkflowDefinitionId, ActionKey, Name, FromStageId, ToStageId, Status)
VALUES (source.WorkflowTransitionId, source.TenantId, source.WorkflowDefinitionId, source.ActionKey, source.Name, source.FromStageId, source.ToStageId, source.Status);

MERGE dbo.WorkflowRoutingRules AS target
USING (VALUES
    ('99999999-aaaa-bbbb-cccc-000000000301', @TenantId, @TransitionCreateByPresalesId, N'DynamicResolver', NULL, NULL, NULL, N'DepartmentIntakeRoute', N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000302', @TenantId, @TransitionForwardRecruiterId, N'Group', NULL, @RecruitingDeliveryGroupId, NULL, NULL, N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000303', @TenantId, @TransitionInterviewId, N'DynamicResolver', NULL, NULL, NULL, N'CandidateInterviewRounds', N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000304', @TenantId, @TransitionHiringManagerId, N'DynamicResolver', NULL, NULL, NULL, N'JobRequestHiringManager', N'Active')
) AS source (WorkflowRoutingRuleId, TenantId, WorkflowTransitionId, AssignmentType, TargetUserId, TargetGroupId, TargetRoleId, ResolverKey, Status)
ON target.WorkflowRoutingRuleId = source.WorkflowRoutingRuleId
WHEN MATCHED THEN UPDATE SET AssignmentType = source.AssignmentType, TargetUserId = source.TargetUserId, TargetGroupId = source.TargetGroupId, TargetRoleId = source.TargetRoleId, ResolverKey = source.ResolverKey, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (WorkflowRoutingRuleId, TenantId, WorkflowTransitionId, AssignmentType, TargetUserId, TargetGroupId, TargetRoleId, ResolverKey, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.WorkflowRoutingRuleId, source.TenantId, source.WorkflowTransitionId, source.AssignmentType, source.TargetUserId, source.TargetGroupId, source.TargetRoleId, source.ResolverKey, source.Status, @Now, @Now);

MERGE dbo.JobRequestIntakeRoutingRules AS target
USING (VALUES
    ('99999999-aaaa-bbbb-cccc-000000000401', @TenantId, @EngineeringDepartmentId, N'Group', NULL, @PmoEngineeringGroupId, N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000402', @TenantId, @QaDepartmentId, N'Group', NULL, @PmoQaGroupId, N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000403', @TenantId, @DevOpsDepartmentId, N'Group', NULL, @PmoDevOpsGroupId, N'Active')
) AS source (JobRequestIntakeRoutingRuleId, TenantId, DepartmentId, AssignmentType, TargetUserId, TargetGroupId, Status)
ON target.TenantId = source.TenantId AND target.DepartmentId = source.DepartmentId
WHEN MATCHED THEN UPDATE SET AssignmentType = source.AssignmentType, TargetUserId = source.TargetUserId, TargetGroupId = source.TargetGroupId, Status = source.Status, UpdatedAtUtc = @Now, UpdatedByUserId = @TenantAdminUserId
WHEN NOT MATCHED THEN INSERT (JobRequestIntakeRoutingRuleId, TenantId, DepartmentId, AssignmentType, TargetUserId, TargetGroupId, Status, CreatedAtUtc, UpdatedAtUtc, UpdatedByUserId)
VALUES (source.JobRequestIntakeRoutingRuleId, source.TenantId, source.DepartmentId, source.AssignmentType, source.TargetUserId, source.TargetGroupId, source.Status, @Now, @Now, @TenantAdminUserId);

MERGE dbo.WorkflowAssignments AS target
USING (VALUES
    (@InitialAssignmentId, @TenantId, @WorkflowDefinitionId, @StagePmoReviewId, @TransitionCreateByPresalesId, N'JobRequest', @JobRequestId, NULL, @PmoEngineeringGroupId, NULL, N'Pending', NULL, DATEADD(HOUR, -5, @Now), CAST(NULL AS DATETIME2(3)), CAST(NULL AS DATETIME2(3)))
) AS source (WorkflowAssignmentId, TenantId, WorkflowDefinitionId, WorkflowStageId, WorkflowTransitionId, EntityType, EntityId, AssignedToUserId, AssignedToGroupId, AssignedToRoleId, AssignmentStatus, ClaimedByUserId, AssignedAtUtc, ClaimedAtUtc, CompletedAtUtc)
ON target.WorkflowAssignmentId = source.WorkflowAssignmentId
WHEN MATCHED THEN UPDATE SET WorkflowStageId = source.WorkflowStageId, AssignmentStatus = source.AssignmentStatus
WHEN NOT MATCHED THEN INSERT (WorkflowAssignmentId, TenantId, WorkflowDefinitionId, WorkflowStageId, WorkflowTransitionId, EntityType, EntityId, AssignedToUserId, AssignedToGroupId, AssignedToRoleId, AssignmentStatus, ClaimedByUserId, AssignedAtUtc, ClaimedAtUtc, CompletedAtUtc)
VALUES (source.WorkflowAssignmentId, source.TenantId, source.WorkflowDefinitionId, source.WorkflowStageId, source.WorkflowTransitionId, source.EntityType, source.EntityId, source.AssignedToUserId, source.AssignedToGroupId, source.AssignedToRoleId, source.AssignmentStatus, source.ClaimedByUserId, source.AssignedAtUtc, source.ClaimedAtUtc, source.CompletedAtUtc);

UPDATE dbo.JobRequests
SET CurrentAssignmentId = @InitialAssignmentId
WHERE TenantId = @TenantId AND JobRequestId = @JobRequestId;

MERGE dbo.InterviewTemplates AS target
USING (VALUES (@InterviewTemplateId, @TenantId, @EngineeringDepartmentId, N'Senior Software Engineer Interview', N'Starter interview template recruiters can copy and customize per job post.', N'Active'))
AS source (InterviewTemplateId, TenantId, DepartmentId, Name, Description, Status)
ON target.InterviewTemplateId = source.InterviewTemplateId
WHEN MATCHED THEN UPDATE SET Name = source.Name, Description = source.Description, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (InterviewTemplateId, TenantId, DepartmentId, Name, Description, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.InterviewTemplateId, source.TenantId, source.DepartmentId, source.Name, source.Description, source.Status, @Now, @Now);

MERGE dbo.InterviewTemplateRounds AS target
USING (VALUES
    (@RoundScreeningId, @TenantId, @InterviewTemplateId, 1, N'HR Screening', @RecruiterRoleId, @RecruiterUserId, 30, CAST(1 AS BIT), N'Active'),
    (@RoundTechnicalId, @TenantId, @InterviewTemplateId, 2, N'Technical Interview', @InterviewerRoleId, @InterviewerUserId, 60, CAST(1 AS BIT), N'Active'),
    (@RoundDepartmentHeadId, @TenantId, @InterviewTemplateId, 3, N'Department Head Interview', @HodRoleId, @HodUserId, 45, CAST(1 AS BIT), N'Active')
) AS source (InterviewTemplateRoundId, TenantId, InterviewTemplateId, RoundOrder, Name, OwnerRoleId, OwnerUserId, DurationMinutes, IsRequired, Status)
ON target.InterviewTemplateRoundId = source.InterviewTemplateRoundId
WHEN MATCHED THEN UPDATE SET Name = source.Name, OwnerRoleId = source.OwnerRoleId, OwnerUserId = source.OwnerUserId, DurationMinutes = source.DurationMinutes, IsRequired = source.IsRequired, Status = source.Status
WHEN NOT MATCHED THEN INSERT (InterviewTemplateRoundId, TenantId, InterviewTemplateId, RoundOrder, Name, OwnerRoleId, OwnerUserId, DurationMinutes, IsRequired, Status)
VALUES (source.InterviewTemplateRoundId, source.TenantId, source.InterviewTemplateId, source.RoundOrder, source.Name, source.OwnerRoleId, source.OwnerUserId, source.DurationMinutes, source.IsRequired, source.Status);

MERGE dbo.JobRequestInterviewRounds AS target
USING (VALUES
    (@JobRoundScreeningId, @TenantId, @JobRequestId, @RoundScreeningId, 1, N'HR Screening', @RecruiterRoleId, @RecruiterUserId, N'Pending'),
    (@JobRoundTechnicalId, @TenantId, @JobRequestId, @RoundTechnicalId, 2, N'Technical Interview', @InterviewerRoleId, @InterviewerUserId, N'Pending'),
    (@JobRoundDepartmentHeadId, @TenantId, @JobRequestId, @RoundDepartmentHeadId, 3, N'Department Head Interview', @HodRoleId, @HodUserId, N'Pending')
) AS source (JobRequestInterviewRoundId, TenantId, JobRequestId, InterviewTemplateRoundId, RoundOrder, Name, OwnerRoleId, OwnerUserId, Status)
ON target.JobRequestInterviewRoundId = source.JobRequestInterviewRoundId
WHEN MATCHED THEN UPDATE SET Name = source.Name, OwnerRoleId = source.OwnerRoleId, OwnerUserId = source.OwnerUserId, Status = source.Status
WHEN NOT MATCHED THEN INSERT (JobRequestInterviewRoundId, TenantId, JobRequestId, InterviewTemplateRoundId, RoundOrder, Name, OwnerRoleId, OwnerUserId, Status)
VALUES (source.JobRequestInterviewRoundId, source.TenantId, source.JobRequestId, source.InterviewTemplateRoundId, source.RoundOrder, source.Name, source.OwnerRoleId, source.OwnerUserId, source.Status);

MERGE dbo.AiRecommendationLogs AS target
USING (VALUES
    ('12345678-aaaa-bbbb-cccc-000000000001', @TenantId, N'bench-matching', N'JobRequest', @JobRequestId, N'Employee', @HamzaEmployeeId, CAST(0.9200 AS DECIMAL(8,4)), N'Benched .NET engineer with SQL Server experience.', N'{"priority":"benched-employee"}'),
    ('12345678-aaaa-bbbb-cccc-000000000002', @TenantId, N'bench-matching', N'JobRequest', @JobRequestId, N'Employee', @AminaEmployeeId, CAST(0.7400 AS DECIMAL(8,4)), N'Benched frontend engineer with Angular experience.', N'{"priority":"benched-employee"}')
) AS source (AiRecommendationLogId, TenantId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, RecommendedEntityType, RecommendedEntityId, Score, Explanation, PayloadJson)
ON target.AiRecommendationLogId = source.AiRecommendationLogId
WHEN MATCHED THEN UPDATE SET Score = source.Score, Explanation = source.Explanation, PayloadJson = source.PayloadJson, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (AiRecommendationLogId, TenantId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, RecommendedEntityType, RecommendedEntityId, Score, Explanation, PayloadJson, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.AiRecommendationLogId, source.TenantId, source.AiAgentDefinitionId, source.SourceEntityType, source.SourceEntityId, source.RecommendedEntityType, source.RecommendedEntityId, source.Score, source.Explanation, source.PayloadJson, @Now, @Now);

MERGE dbo.AuditLogs AS target
USING (VALUES
    ('99999999-9999-9999-9999-999999999902', @TenantId, @TenantAdminUserId, N'Mudasar Ahmad', N'database.seed.domain', N'Schema', @TenantId, N'Talent Pilot domain schema', N'Domain reference data was seeded from the Database/Schema discussion package.', N'Database', N'{"seed":"002_seed_domain_reference_data"}')
) AS source (AuditLogId, TenantId, ActorUserId, ActorDisplayName, EventType, EntityType, EntityId, RecordLabel, EventSummary, Area, MetadataJson)
ON target.AuditLogId = source.AuditLogId
WHEN MATCHED THEN UPDATE SET ActorUserId = source.ActorUserId, ActorDisplayName = source.ActorDisplayName, EventSummary = source.EventSummary, MetadataJson = source.MetadataJson
WHEN NOT MATCHED THEN INSERT (AuditLogId, TenantId, OccurredAtUtc, ActorUserId, ActorDisplayName, EventType, EntityType, EntityId, RecordLabel, EventSummary, Area, MetadataJson)
VALUES (source.AuditLogId, source.TenantId, @Now, source.ActorUserId, source.ActorDisplayName, source.EventType, source.EntityType, source.EntityId, source.RecordLabel, source.EventSummary, source.Area, source.MetadataJson);
GO
