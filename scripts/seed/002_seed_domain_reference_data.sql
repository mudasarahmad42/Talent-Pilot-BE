SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

DECLARE @TenantAdminRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222201';
DECLARE @PresalesRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222202';
DECLARE @PmoRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222203';
DECLARE @RecruiterRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222204';
DECLARE @InterviewerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222205';
DECLARE @HiringManagerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222206';
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222207';

DECLARE @TenantAdminUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333301';
DECLARE @PresalesUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333302';
DECLARE @PmoUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333303';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @CandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333307';

DECLARE @PmoGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444401';
DECLARE @RecruitingGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444402';
DECLARE @InterviewPanelGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444403';

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

DECLARE @AliEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd01';
DECLARE @BilalEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd02';
DECLARE @FatimaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd03';
DECLARE @HamzaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd04';
DECLARE @AminaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd05';
DECLARE @UsmanEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd06';

DECLARE @ProjectPhoenixId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01';
DECLARE @ProjectAtlasId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02';
DECLARE @AssignmentBilalId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee21';
DECLARE @AssignmentFatimaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee22';

DECLARE @JobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee01';
DECLARE @CandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee11';
DECLARE @CandidateProspectId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee12';
DECLARE @CandidateInvitationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee13';
DECLARE @JobApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee14';

DECLARE @InterviewTemplateId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff01';
DECLARE @RoundScreeningId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff11';
DECLARE @RoundTechnicalId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff12';
DECLARE @RoundHiringManagerId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff13';
DECLARE @JobRoundScreeningId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff21';
DECLARE @JobRoundTechnicalId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff22';
DECLARE @JobRoundHiringManagerId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff23';

DECLARE @WorkflowDefinitionId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000001';
DECLARE @StageDraftId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000011';
DECLARE @StagePmoReviewId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000012';
DECLARE @StageSourcingId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000013';
DECLARE @StageInterviewingId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000014';
DECLARE @StageHiringManagerId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000015';
DECLARE @StageOfferId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000016';
DECLARE @StageClosedId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000017';
DECLARE @TransitionCreateByPresalesId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000101';
DECLARE @TransitionForwardRecruiterId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000102';
DECLARE @TransitionInterviewId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000103';
DECLARE @TransitionHiringManagerId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000104';
DECLARE @InitialAssignmentId UNIQUEIDENTIFIER = '99999999-aaaa-bbbb-cccc-000000000201';

MERGE dbo.TenantAiSettings AS target
USING (VALUES
    (@TenantId, N'Mock/Ollama', N'llama3.1:8b', N'nomic-embed-text', 768, N'SqlServerVector', CAST(1 AS BIT), CAST(1 AS BIT), CAST(0 AS BIT), CAST(0 AS BIT))
) AS source (TenantId, ProviderMode, LlmModel, EmbeddingModel, EmbeddingDimensions, VectorStore, ModelSwitchingLocked, HumanReviewRequired, AutoRejectEnabled, AutomaticStageMovementEnabled)
ON target.TenantId = source.TenantId
WHEN MATCHED THEN
    UPDATE SET ProviderMode = source.ProviderMode, LlmModel = source.LlmModel, EmbeddingModel = source.EmbeddingModel, EmbeddingDimensions = source.EmbeddingDimensions,
        VectorStore = source.VectorStore, ModelSwitchingLocked = source.ModelSwitchingLocked, HumanReviewRequired = source.HumanReviewRequired,
        AutoRejectEnabled = source.AutoRejectEnabled, AutomaticStageMovementEnabled = source.AutomaticStageMovementEnabled, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (TenantId, ProviderMode, LlmModel, EmbeddingModel, EmbeddingDimensions, VectorStore, ModelSwitchingLocked, HumanReviewRequired, AutoRejectEnabled, AutomaticStageMovementEnabled, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.TenantId, source.ProviderMode, source.LlmModel, source.EmbeddingModel, source.EmbeddingDimensions, source.VectorStore, source.ModelSwitchingLocked, source.HumanReviewRequired, source.AutoRejectEnabled, source.AutomaticStageMovementEnabled, @Now, @Now);

MERGE dbo.Departments AS target
USING (VALUES
    (@EngineeringDepartmentId, @TenantId, N'ENG', N'Engineering', @HiringManagerUserId, N'Active'),
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
    (@OtherSourceLabelId, @TenantId, N'Other', N'Other', N'Manual review', N'Active')
) AS source (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status)
ON target.CandidateSourceLabelId = source.CandidateSourceLabelId
WHEN MATCHED THEN UPDATE SET Code = source.Code, DisplayName = source.DisplayName, ReportingCategory = source.ReportingCategory, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.CandidateSourceLabelId, source.TenantId, source.Code, source.DisplayName, source.ReportingCategory, source.Status, @Now, @Now);

MERGE dbo.Projects AS target
USING (VALUES
    (@ProjectPhoenixId, @TenantId, @EngineeringDepartmentId, N'PHX', N'Phoenix Delivery Platform', N'Confidential Client', N'Active', CONVERT(date, '2026-01-01'), NULL),
    (@ProjectAtlasId, @TenantId, @EngineeringDepartmentId, N'ATL', N'Atlas Modernization', N'Enterprise Client', N'Active', CONVERT(date, '2026-02-01'), NULL)
) AS source (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn)
ON target.ProjectId = source.ProjectId
WHEN MATCHED THEN UPDATE SET DepartmentId = source.DepartmentId, Code = source.Code, Name = source.Name, ClientName = source.ClientName, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.ProjectId, source.TenantId, source.DepartmentId, source.Code, source.Name, source.ClientName, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);

MERGE dbo.Employees AS target
USING (VALUES
    (@AliEmployeeId, @TenantId, @PmoUserId, N'TKX-1001', N'EXT-1001', N'Ali Khan', N'pmo@tkxel.com', @PmoDepartmentId, @KarachiLocationId, N'PMO Manager', CAST(8.0 AS DECIMAL(4,1)), N'Allocated', N'Allocated', N'Active'),
    (@BilalEmployeeId, @TenantId, @InterviewerUserId, N'TKX-1002', N'EXT-1002', N'Bilal Hussain', N'interviewer@tkxel.com', @EngineeringDepartmentId, @KarachiLocationId, N'Senior Software Engineer', CAST(6.0 AS DECIMAL(4,1)), N'Allocated', N'Allocated', N'Active'),
    (@FatimaEmployeeId, @TenantId, @HiringManagerUserId, N'TKX-1003', N'EXT-1003', N'Fatima Noor', N'hiring.manager@tkxel.com', @EngineeringDepartmentId, @LahoreLocationId, N'Engineering Manager', CAST(10.0 AS DECIMAL(4,1)), N'Allocated', N'Allocated', N'Active'),
    (@HamzaEmployeeId, @TenantId, NULL, N'TKX-1004', N'EXT-1004', N'Hamza Ali', N'hamza.ali@tkxel.com', @EngineeringDepartmentId, @KarachiLocationId, N'Senior .NET Engineer', CAST(5.5 AS DECIMAL(4,1)), N'Available', N'Benched', N'Active'),
    (@AminaEmployeeId, @TenantId, NULL, N'TKX-1005', N'EXT-1005', N'Amina Shah', N'amina.shah@tkxel.com', @EngineeringDepartmentId, @RemoteLocationId, N'Angular Engineer', CAST(4.0 AS DECIMAL(4,1)), N'Available', N'Benched', N'Active'),
    (@UsmanEmployeeId, @TenantId, NULL, N'TKX-1006', N'EXT-1006', N'Usman Tariq', N'usman.tariq@tkxel.com', @DevOpsDepartmentId, @LahoreLocationId, N'DevOps Engineer', CAST(5.0 AS DECIMAL(4,1)), N'Available', N'Benched', N'Active')
) AS source (EmployeeId, TenantId, AppUserId, EmployeeCode, ExternalEmployeeId, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, AvailabilityStatus, BenchStatus, Status)
ON target.EmployeeId = source.EmployeeId
WHEN MATCHED THEN UPDATE SET AppUserId = source.AppUserId, EmployeeCode = source.EmployeeCode, ExternalEmployeeId = source.ExternalEmployeeId, DisplayName = source.DisplayName,
    Email = source.Email, DepartmentId = source.DepartmentId, LocationId = source.LocationId, Designation = source.Designation, ExperienceYears = source.ExperienceYears,
    AvailabilityStatus = source.AvailabilityStatus, BenchStatus = source.BenchStatus, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (EmployeeId, TenantId, AppUserId, EmployeeCode, ExternalEmployeeId, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, AvailabilityStatus, BenchStatus, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.EmployeeId, source.TenantId, source.AppUserId, source.EmployeeCode, source.ExternalEmployeeId, source.DisplayName, source.Email, source.DepartmentId, source.LocationId, source.Designation, source.ExperienceYears, source.AvailabilityStatus, source.BenchStatus, source.Status, @Now, @Now);

MERGE dbo.EmployeeProjectAssignments AS target
USING (VALUES
    (@AssignmentBilalId, @TenantId, @BilalEmployeeId, @ProjectPhoenixId, 100, N'Active', CONVERT(date, '2026-01-01'), NULL),
    (@AssignmentFatimaId, @TenantId, @FatimaEmployeeId, @ProjectAtlasId, 100, N'Active', CONVERT(date, '2026-02-01'), NULL)
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
    (@TenantId, @BilalEmployeeId, @AzureSkillId, N'Intermediate', CAST(3.0 AS DECIMAL(4,1)), CAST(0 AS BIT))
) AS source (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary)
ON target.TenantId = source.TenantId AND target.EmployeeId = source.EmployeeId AND target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
WHEN NOT MATCHED THEN INSERT (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
VALUES (source.TenantId, source.EmployeeId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary, @Now);

MERGE dbo.Candidates AS target
USING (VALUES
    (@CandidateId, @TenantId, @CandidateUserId, N'Ayesha Khan', N'ayesha.khan@example.com', N'+92-300-0000000', N'https://linkedin.com/in/ayesha-khan', N'Senior Software Engineer', N'Previous Employer', CAST(5.0 AS DECIMAL(4,1)), CAST(450000 AS DECIMAL(18,2)), 'PKR', 30, N'Active')
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
    (@TenantId, @CandidateId, @AngularSkillId, N'Intermediate', CAST(2.0 AS DECIMAL(4,1)), CAST(0 AS BIT))
) AS source (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary)
ON target.TenantId = source.TenantId AND target.CandidateId = source.CandidateId AND target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
WHEN NOT MATCHED THEN INSERT (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
VALUES (source.TenantId, source.CandidateId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary, @Now);

MERGE dbo.JobRequests AS target
USING (VALUES
    (@JobRequestId, @TenantId, N'TP-REQ-0001', N'Senior .NET Engineer', N'Client needs a senior .NET engineer with SQL Server, Azure, and Angular exposure.', N'Enterprise Client', @EngineeringDepartmentId, @KarachiLocationId, N'FullTime', CAST(5.0 AS DECIMAL(4,1)), CAST(8.0 AS DECIMAL(4,1)), N'High', 1, 0, N'PMOReview', N'Published', @HiringManagerUserId, @InterviewPanelGroupId, @PresalesUserId, N'PMO_REVIEW', DATEADD(DAY, -1, @Now))
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
    (@TenantId, @JobRequestId, @AngularSkillId, CAST(0 AS BIT), 5)
) AS source (TenantId, JobRequestId, SkillId, IsRequired, Weight)
ON target.TenantId = source.TenantId AND target.JobRequestId = source.JobRequestId AND target.SkillId = source.SkillId
WHEN MATCHED THEN UPDATE SET IsRequired = source.IsRequired, Weight = source.Weight
WHEN NOT MATCHED THEN INSERT (TenantId, JobRequestId, SkillId, IsRequired, Weight, CreatedAtUtc)
VALUES (source.TenantId, source.JobRequestId, source.SkillId, source.IsRequired, source.Weight, @Now);

MERGE dbo.CandidateProspects AS target
USING (VALUES
    (@CandidateProspectId, @TenantId, N'Ayesha Khan', N'ayesha.khan@example.com', N'+92-300-0000000', N'https://linkedin.com/in/ayesha-khan', @LinkedInSourceLabelId, N'LinkedIn', N'Registered', @CandidateId, @RecruiterUserId)
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

MERGE dbo.CandidateInvitations AS target
USING (VALUES
    (@CandidateInvitationId, @TenantId, @CandidateProspectId, @CandidateId, @JobRequestId, @RecruiterUserId, N'demo-token-hash-only', N'ayesha.khan@example.com', N'Used', DATEADD(DAY, 7, @Now), DATEADD(HOUR, -6, @Now), CAST(NULL AS DATETIME2(3)), 0)
) AS source (CandidateInvitationId, TenantId, CandidateProspectId, CandidateId, JobRequestId, InvitedByUserId, TokenHash, Email, Status, ExpiresAtUtc, UsedAtUtc, RevokedAtUtc, ResendCount)
ON target.CandidateInvitationId = source.CandidateInvitationId
WHEN MATCHED THEN UPDATE SET Status = source.Status, UsedAtUtc = source.UsedAtUtc, ResendCount = source.ResendCount
WHEN NOT MATCHED THEN INSERT (CandidateInvitationId, TenantId, CandidateProspectId, CandidateId, JobRequestId, InvitedByUserId, TokenHash, Email, Status, ExpiresAtUtc, UsedAtUtc, RevokedAtUtc, ResendCount, CreatedAtUtc)
VALUES (source.CandidateInvitationId, source.TenantId, source.CandidateProspectId, source.CandidateId, source.JobRequestId, source.InvitedByUserId, source.TokenHash, source.Email, source.Status, source.ExpiresAtUtc, source.UsedAtUtc, source.RevokedAtUtc, source.ResendCount, @Now);

MERGE dbo.JobApplications AS target
USING (VALUES
    (@JobApplicationId, @TenantId, @JobRequestId, @CandidateId, @LinkedInSourceLabelId, N'LinkedIn', N'Interviewing', 1, CAST(1 AS BIT), CAST(1 AS BIT), DATEADD(HOUR, -6, @Now), DATEADD(HOUR, -6, @Now), CAST(NULL AS DATETIME2(3)), CAST(NULL AS NVARCHAR(500)))
) AS source (JobApplicationId, TenantId, JobRequestId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason)
ON target.JobApplicationId = source.JobApplicationId
WHEN MATCHED THEN UPDATE SET CandidateSourceLabelId = source.CandidateSourceLabelId, SourceLabel = source.SourceLabel, CurrentStatus = source.CurrentStatus, IsActive = source.IsActive, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (JobApplicationId, TenantId, JobRequestId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.JobApplicationId, source.TenantId, source.JobRequestId, source.CandidateId, source.CandidateSourceLabelId, source.SourceLabel, source.CurrentStatus, source.ApplicationVersion, source.IsActive, source.IsInvited, source.ConfirmedAtUtc, source.AppliedAtUtc, source.FinalDecisionAtUtc, source.FinalDecisionReason, @Now, @Now);

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
    (@StageSourcingId, @TenantId, @WorkflowDefinitionId, N'SOURCING', N'Recruiter Sourcing', 30, CAST(0 AS BIT), N'Active'),
    (@StageInterviewingId, @TenantId, @WorkflowDefinitionId, N'INTERVIEWING', N'Interviewing', 40, CAST(0 AS BIT), N'Active'),
    (@StageHiringManagerId, @TenantId, @WorkflowDefinitionId, N'HIRING_MANAGER_REVIEW', N'Hiring Manager Review', 50, CAST(0 AS BIT), N'Active'),
    (@StageOfferId, @TenantId, @WorkflowDefinitionId, N'OFFER', N'Offer', 60, CAST(0 AS BIT), N'Active'),
    (@StageClosedId, @TenantId, @WorkflowDefinitionId, N'CLOSED', N'Closed', 70, CAST(1 AS BIT), N'Active')
) AS source (WorkflowStageId, TenantId, WorkflowDefinitionId, StageKey, Name, StageOrder, IsTerminal, Status)
ON target.WorkflowStageId = source.WorkflowStageId
WHEN MATCHED THEN UPDATE SET StageKey = source.StageKey, Name = source.Name, StageOrder = source.StageOrder, IsTerminal = source.IsTerminal, Status = source.Status
WHEN NOT MATCHED THEN INSERT (WorkflowStageId, TenantId, WorkflowDefinitionId, StageKey, Name, StageOrder, IsTerminal, Status)
VALUES (source.WorkflowStageId, source.TenantId, source.WorkflowDefinitionId, source.StageKey, source.Name, source.StageOrder, source.IsTerminal, source.Status);

MERGE dbo.WorkflowTransitions AS target
USING (VALUES
    (@TransitionCreateByPresalesId, @TenantId, @WorkflowDefinitionId, N'CREATE_BY_PRESALES', N'Create by Presales', @StageDraftId, @StagePmoReviewId, N'Active'),
    (@TransitionForwardRecruiterId, @TenantId, @WorkflowDefinitionId, N'FORWARD_TO_RECRUITER', N'Forward to Recruiter', @StagePmoReviewId, @StageSourcingId, N'Active'),
    (@TransitionInterviewId, @TenantId, @WorkflowDefinitionId, N'MOVE_TO_INTERVIEWING', N'Move to Interviewing', @StageSourcingId, @StageInterviewingId, N'Active'),
    (@TransitionHiringManagerId, @TenantId, @WorkflowDefinitionId, N'FORWARD_TO_HIRING_MANAGER', N'Forward to Hiring Manager', @StageInterviewingId, @StageHiringManagerId, N'Active')
) AS source (WorkflowTransitionId, TenantId, WorkflowDefinitionId, ActionKey, Name, FromStageId, ToStageId, Status)
ON target.WorkflowTransitionId = source.WorkflowTransitionId
WHEN MATCHED THEN UPDATE SET ActionKey = source.ActionKey, Name = source.Name, FromStageId = source.FromStageId, ToStageId = source.ToStageId, Status = source.Status
WHEN NOT MATCHED THEN INSERT (WorkflowTransitionId, TenantId, WorkflowDefinitionId, ActionKey, Name, FromStageId, ToStageId, Status)
VALUES (source.WorkflowTransitionId, source.TenantId, source.WorkflowDefinitionId, source.ActionKey, source.Name, source.FromStageId, source.ToStageId, source.Status);

MERGE dbo.WorkflowRoutingRules AS target
USING (VALUES
    ('99999999-aaaa-bbbb-cccc-000000000301', @TenantId, @TransitionCreateByPresalesId, N'Group', NULL, @PmoGroupId, NULL, NULL, N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000302', @TenantId, @TransitionForwardRecruiterId, N'Group', NULL, @RecruitingGroupId, NULL, NULL, N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000303', @TenantId, @TransitionInterviewId, N'Group', NULL, @InterviewPanelGroupId, NULL, NULL, N'Active'),
    ('99999999-aaaa-bbbb-cccc-000000000304', @TenantId, @TransitionHiringManagerId, N'User', @HiringManagerUserId, NULL, NULL, NULL, N'Active')
) AS source (WorkflowRoutingRuleId, TenantId, WorkflowTransitionId, AssignmentType, TargetUserId, TargetGroupId, TargetRoleId, ResolverKey, Status)
ON target.WorkflowRoutingRuleId = source.WorkflowRoutingRuleId
WHEN MATCHED THEN UPDATE SET AssignmentType = source.AssignmentType, TargetUserId = source.TargetUserId, TargetGroupId = source.TargetGroupId, TargetRoleId = source.TargetRoleId, ResolverKey = source.ResolverKey, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (WorkflowRoutingRuleId, TenantId, WorkflowTransitionId, AssignmentType, TargetUserId, TargetGroupId, TargetRoleId, ResolverKey, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.WorkflowRoutingRuleId, source.TenantId, source.WorkflowTransitionId, source.AssignmentType, source.TargetUserId, source.TargetGroupId, source.TargetRoleId, source.ResolverKey, source.Status, @Now, @Now);

MERGE dbo.WorkflowActionPermissions AS target
USING (VALUES
    (@TenantId, @TransitionCreateByPresalesId, @PresalesRoleId, CAST(0 AS BIT), CAST(0 AS BIT)),
    (@TenantId, @TransitionForwardRecruiterId, @PmoRoleId, CAST(1 AS BIT), CAST(1 AS BIT)),
    (@TenantId, @TransitionInterviewId, @RecruiterRoleId, CAST(1 AS BIT), CAST(1 AS BIT)),
    (@TenantId, @TransitionHiringManagerId, @InterviewerRoleId, CAST(1 AS BIT), CAST(1 AS BIT)),
    (@TenantId, @TransitionHiringManagerId, @RecruiterRoleId, CAST(1 AS BIT), CAST(1 AS BIT)),
    (@TenantId, @TransitionCreateByPresalesId, @TenantAdminRoleId, CAST(0 AS BIT), CAST(0 AS BIT)),
    (@TenantId, @TransitionForwardRecruiterId, @TenantAdminRoleId, CAST(0 AS BIT), CAST(0 AS BIT))
) AS source (TenantId, WorkflowTransitionId, RoleId, MustBeCurrentAssignee, MustBeGroupMember)
ON target.TenantId = source.TenantId AND target.WorkflowTransitionId = source.WorkflowTransitionId AND target.RoleId = source.RoleId
WHEN MATCHED THEN UPDATE SET MustBeCurrentAssignee = source.MustBeCurrentAssignee, MustBeGroupMember = source.MustBeGroupMember
WHEN NOT MATCHED THEN INSERT (TenantId, WorkflowTransitionId, RoleId, MustBeCurrentAssignee, MustBeGroupMember, CreatedAtUtc)
VALUES (source.TenantId, source.WorkflowTransitionId, source.RoleId, source.MustBeCurrentAssignee, source.MustBeGroupMember, @Now);

MERGE dbo.WorkflowAssignments AS target
USING (VALUES
    (@InitialAssignmentId, @TenantId, @WorkflowDefinitionId, @StagePmoReviewId, @TransitionCreateByPresalesId, N'JobRequest', @JobRequestId, NULL, @PmoGroupId, NULL, N'Pending', NULL, DATEADD(HOUR, -5, @Now), CAST(NULL AS DATETIME2(3)), CAST(NULL AS DATETIME2(3)))
) AS source (WorkflowAssignmentId, TenantId, WorkflowDefinitionId, WorkflowStageId, WorkflowTransitionId, EntityType, EntityId, AssignedToUserId, AssignedToGroupId, AssignedToRoleId, AssignmentStatus, ClaimedByUserId, AssignedAtUtc, ClaimedAtUtc, CompletedAtUtc)
ON target.WorkflowAssignmentId = source.WorkflowAssignmentId
WHEN MATCHED THEN UPDATE SET WorkflowStageId = source.WorkflowStageId, AssignmentStatus = source.AssignmentStatus
WHEN NOT MATCHED THEN INSERT (WorkflowAssignmentId, TenantId, WorkflowDefinitionId, WorkflowStageId, WorkflowTransitionId, EntityType, EntityId, AssignedToUserId, AssignedToGroupId, AssignedToRoleId, AssignmentStatus, ClaimedByUserId, AssignedAtUtc, ClaimedAtUtc, CompletedAtUtc)
VALUES (source.WorkflowAssignmentId, source.TenantId, source.WorkflowDefinitionId, source.WorkflowStageId, source.WorkflowTransitionId, source.EntityType, source.EntityId, source.AssignedToUserId, source.AssignedToGroupId, source.AssignedToRoleId, source.AssignmentStatus, source.ClaimedByUserId, source.AssignedAtUtc, source.ClaimedAtUtc, source.CompletedAtUtc);

UPDATE dbo.JobRequests
SET CurrentAssignmentId = @InitialAssignmentId
WHERE TenantId = @TenantId AND JobRequestId = @JobRequestId;

MERGE dbo.InterviewTemplates AS target
USING (VALUES (@InterviewTemplateId, @TenantId, @EngineeringDepartmentId, N'Senior Software Engineer Interview', N'Fixed MVP template used by recruiter when creating a job post.', N'Active'))
AS source (InterviewTemplateId, TenantId, DepartmentId, Name, Description, Status)
ON target.InterviewTemplateId = source.InterviewTemplateId
WHEN MATCHED THEN UPDATE SET Name = source.Name, Description = source.Description, Status = source.Status, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (InterviewTemplateId, TenantId, DepartmentId, Name, Description, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES (source.InterviewTemplateId, source.TenantId, source.DepartmentId, source.Name, source.Description, source.Status, @Now, @Now);

MERGE dbo.InterviewTemplateRounds AS target
USING (VALUES
    (@RoundScreeningId, @TenantId, @InterviewTemplateId, 1, N'HR Screening', @RecruiterRoleId, 30, CAST(1 AS BIT), N'Active'),
    (@RoundTechnicalId, @TenantId, @InterviewTemplateId, 2, N'Technical Interview', @InterviewerRoleId, 60, CAST(1 AS BIT), N'Active'),
    (@RoundHiringManagerId, @TenantId, @InterviewTemplateId, 3, N'Hiring Manager Review', @HiringManagerRoleId, 45, CAST(1 AS BIT), N'Active')
) AS source (InterviewTemplateRoundId, TenantId, InterviewTemplateId, RoundOrder, Name, OwnerRoleId, DurationMinutes, IsRequired, Status)
ON target.InterviewTemplateRoundId = source.InterviewTemplateRoundId
WHEN MATCHED THEN UPDATE SET Name = source.Name, OwnerRoleId = source.OwnerRoleId, DurationMinutes = source.DurationMinutes, IsRequired = source.IsRequired, Status = source.Status
WHEN NOT MATCHED THEN INSERT (InterviewTemplateRoundId, TenantId, InterviewTemplateId, RoundOrder, Name, OwnerRoleId, DurationMinutes, IsRequired, Status)
VALUES (source.InterviewTemplateRoundId, source.TenantId, source.InterviewTemplateId, source.RoundOrder, source.Name, source.OwnerRoleId, source.DurationMinutes, source.IsRequired, source.Status);

MERGE dbo.JobRequestInterviewRounds AS target
USING (VALUES
    (@JobRoundScreeningId, @TenantId, @JobRequestId, @RoundScreeningId, 1, N'HR Screening', @RecruiterRoleId, N'Pending'),
    (@JobRoundTechnicalId, @TenantId, @JobRequestId, @RoundTechnicalId, 2, N'Technical Interview', @InterviewerRoleId, N'Pending'),
    (@JobRoundHiringManagerId, @TenantId, @JobRequestId, @RoundHiringManagerId, 3, N'Hiring Manager Review', @HiringManagerRoleId, N'Pending')
) AS source (JobRequestInterviewRoundId, TenantId, JobRequestId, InterviewTemplateRoundId, RoundOrder, Name, OwnerRoleId, Status)
ON target.JobRequestInterviewRoundId = source.JobRequestInterviewRoundId
WHEN MATCHED THEN UPDATE SET Name = source.Name, OwnerRoleId = source.OwnerRoleId, Status = source.Status
WHEN NOT MATCHED THEN INSERT (JobRequestInterviewRoundId, TenantId, JobRequestId, InterviewTemplateRoundId, RoundOrder, Name, OwnerRoleId, Status)
VALUES (source.JobRequestInterviewRoundId, source.TenantId, source.JobRequestId, source.InterviewTemplateRoundId, source.RoundOrder, source.Name, source.OwnerRoleId, source.Status);

MERGE dbo.AiRecommendationLogs AS target
USING (VALUES
    ('12345678-aaaa-bbbb-cccc-000000000001', @TenantId, N'bench-matching', N'JobRequest', @JobRequestId, N'Employee', @HamzaEmployeeId, CAST(0.9200 AS DECIMAL(8,4)), N'Benched .NET engineer with SQL Server experience.', N'{"priority":"benched-employee"}'),
    ('12345678-aaaa-bbbb-cccc-000000000002', @TenantId, N'bench-matching', N'JobRequest', @JobRequestId, N'Employee', @AminaEmployeeId, CAST(0.7400 AS DECIMAL(8,4)), N'Benched frontend engineer with Angular experience.', N'{"priority":"benched-employee"}')
) AS source (AiRecommendationLogId, TenantId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, RecommendedEntityType, RecommendedEntityId, Score, Explanation, PayloadJson)
ON target.AiRecommendationLogId = source.AiRecommendationLogId
WHEN MATCHED THEN UPDATE SET Score = source.Score, Explanation = source.Explanation, PayloadJson = source.PayloadJson
WHEN NOT MATCHED THEN INSERT (AiRecommendationLogId, TenantId, AiAgentDefinitionId, SourceEntityType, SourceEntityId, RecommendedEntityType, RecommendedEntityId, Score, Explanation, PayloadJson, CreatedAtUtc)
VALUES (source.AiRecommendationLogId, source.TenantId, source.AiAgentDefinitionId, source.SourceEntityType, source.SourceEntityId, source.RecommendedEntityType, source.RecommendedEntityId, source.Score, source.Explanation, source.PayloadJson, @Now);

MERGE dbo.AuditLogs AS target
USING (VALUES
    ('99999999-9999-9999-9999-999999999902', @TenantId, @TenantAdminUserId, N'Mudasar Ahmad', N'database.seed.domain', N'Schema', @TenantId, N'Talent Pilot domain schema', N'Domain reference data was seeded from the Database/Schema discussion package.', N'Database', N'{"seed":"002_seed_domain_reference_data"}')
) AS source (AuditLogId, TenantId, ActorUserId, ActorDisplayName, EventType, EntityType, EntityId, RecordLabel, EventSummary, Area, MetadataJson)
ON target.AuditLogId = source.AuditLogId
WHEN MATCHED THEN UPDATE SET ActorUserId = source.ActorUserId, ActorDisplayName = source.ActorDisplayName, EventSummary = source.EventSummary, MetadataJson = source.MetadataJson
WHEN NOT MATCHED THEN INSERT (AuditLogId, TenantId, OccurredAtUtc, ActorUserId, ActorDisplayName, EventType, EntityType, EntityId, RecordLabel, EventSummary, Area, MetadataJson)
VALUES (source.AuditLogId, source.TenantId, @Now, source.ActorUserId, source.ActorDisplayName, source.EventType, source.EntityType, source.EntityId, source.RecordLabel, source.EventSummary, source.Area, source.MetadataJson);
GO
