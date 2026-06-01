/*
    Adds demo historical candidate records for the Talent Rediscovery agent.
    The script is guarded so fresh databases can still rely on the seed scripts
    after migrations run; existing developer databases receive the warm-candidate
    evidence needed to test recruiter-side rediscovery.
*/

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @ReactCandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333308';
DECLARE @AngularCandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333309';
DECLARE @HiredCandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333310';
DECLARE @EngineeringDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';
DECLARE @QaDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02';
DECLARE @KarachiLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01';
DECLARE @LahoreLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02';
DECLARE @RemoteLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03';
DECLARE @AngularSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc01';
DECLARE @DotNetSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc02';
DECLARE @SqlServerSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc03';
DECLARE @AzureSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc04';
DECLARE @ReactSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc05';
DECLARE @QaAutomationSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc06';
DECLARE @LinkedInSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc001';
DECLARE @ReferralSourceLabelId UNIQUEIDENTIFIER = 'abcabcab-abca-abca-abca-abcabcabc003';
DECLARE @ReactCandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee15';
DECLARE @AngularCandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee16';
DECLARE @HiredCandidateId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee17';
DECLARE @HistoricalReactJobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee02';
DECLARE @HistoricalDotNetJobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee03';
DECLARE @HistoricalQaJobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee04';
DECLARE @ReactHistoricalApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee31';
DECLARE @AngularHistoricalApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee32';
DECLARE @HiredHistoricalApplicationId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee33';
DECLARE @ReactHistoricalInterviewId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee41';
DECLARE @AngularHistoricalInterviewId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee42';
DECLARE @ReactHistoricalFeedbackId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee51';
DECLARE @AngularHistoricalFeedbackId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee52';

IF EXISTS (SELECT 1 FROM dbo.AiAgentDefinitions WHERE AiAgentDefinitionId = N'talent-rediscovery')
BEGIN
    UPDATE dbo.AiAgentDefinitions
    SET
        Responsibility = N'Ranks previous warm candidates before external sourcing using candidate skills, historical applications, interview feedback, outcomes, and vector similarity. No web search is used for candidate data.',
        InputSummary = N'Claimed Recruiter Sourcing request or draft Job Post, active tenant candidates with useful historical applications, candidate skills, interview feedback, prior outcomes, and candidate profile embeddings.',
        OutputSummary = N'Ranked warm candidates with score, confidence, matched skills, gaps, prior application evidence, interview evidence, and caveats.',
        MvpBoundary = N'Recruiters review the ranking manually; the agent cannot contact candidates, move workflow stages, or make hiring decisions.',
        Enabled = CAST(1 AS BIT),
        UpdatedAtUtc = @Now
    WHERE AiAgentDefinitionId = N'talent-rediscovery';
END;

IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantId = @TenantId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @RecruiterUserId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @InterviewerUserId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @HiringManagerUserId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = @EngineeringDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = @QaDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Locations WHERE LocationId = @LahoreLocationId)
   AND EXISTS (SELECT 1 FROM dbo.Skills WHERE SkillId = @ReactSkillId)
BEGIN
    MERGE dbo.AppUsers AS target
    USING (VALUES
        (@ReactCandidateUserId, @TenantId, N'Nida Farooq', N'nida.farooq@example.com', N'nida.farooq@example.com', N'NF', N'Active'),
        (@AngularCandidateUserId, @TenantId, N'Omar Sheikh', N'omar.sheikh@example.com', N'omar.sheikh@example.com', N'OS', N'Active'),
        (@HiredCandidateUserId, @TenantId, N'Zara Iqbal', N'zara.iqbal@example.com', N'zara.iqbal@example.com', N'ZI', N'Active')
    ) AS source (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus)
    ON target.UserId = source.UserId
    WHEN MATCHED THEN UPDATE SET DisplayName = source.DisplayName, Email = source.Email, EmailNormalized = source.EmailNormalized, Initials = source.Initials, AccountStatus = source.AccountStatus, UpdatedAtUtc = @Now, DeletedAtUtc = NULL
    WHEN NOT MATCHED THEN INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.UserId, source.TenantId, source.DisplayName, source.Email, source.EmailNormalized, source.Initials, source.AccountStatus, @Now, @Now);

    MERGE dbo.Candidates AS target
    USING (VALUES
        (@ReactCandidateId, @TenantId, @ReactCandidateUserId, N'Nida Farooq', N'nida.farooq@example.com', N'+92-300-0000001', N'https://linkedin.com/in/nida-farooq', N'Senior React Developer', N'Product Studio', CAST(6.5 AS DECIMAL(4,1)), CAST(520000 AS DECIMAL(18,2)), 'PKR', 15, N'Active'),
        (@AngularCandidateId, @TenantId, @AngularCandidateUserId, N'Omar Sheikh', N'omar.sheikh@example.com', N'+92-300-0000002', N'https://linkedin.com/in/omar-sheikh', N'Frontend Engineer', N'Consulting Partner', CAST(4.5 AS DECIMAL(4,1)), CAST(390000 AS DECIMAL(18,2)), 'PKR', 30, N'Active'),
        (@HiredCandidateId, @TenantId, @HiredCandidateUserId, N'Zara Iqbal', N'zara.iqbal@example.com', N'+92-300-0000003', N'https://linkedin.com/in/zara-iqbal', N'QA Automation Engineer', N'Quality Guild', CAST(5.5 AS DECIMAL(4,1)), CAST(360000 AS DECIMAL(18,2)), 'PKR', 0, N'Hired')
    ) AS source (CandidateId, TenantId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, ExpectedSalaryAmount, ExpectedSalaryCurrency, NoticePeriodDays, Status)
    ON target.CandidateId = source.CandidateId
    WHEN MATCHED THEN UPDATE SET DisplayName = source.DisplayName, Email = source.Email, Phone = source.Phone, LinkedInUrl = source.LinkedInUrl, CurrentDesignation = source.CurrentDesignation, CurrentCompany = source.CurrentCompany, ExperienceYears = source.ExperienceYears, ExpectedSalaryAmount = source.ExpectedSalaryAmount, ExpectedSalaryCurrency = source.ExpectedSalaryCurrency, NoticePeriodDays = source.NoticePeriodDays, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN INSERT (CandidateId, TenantId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, ExpectedSalaryAmount, ExpectedSalaryCurrency, NoticePeriodDays, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.CandidateId, source.TenantId, source.AppUserId, source.DisplayName, source.Email, source.Phone, source.LinkedInUrl, source.CurrentDesignation, source.CurrentCompany, source.ExperienceYears, source.ExpectedSalaryAmount, source.ExpectedSalaryCurrency, source.NoticePeriodDays, source.Status, @Now, @Now);

    MERGE dbo.CandidateSkills AS target
    USING (VALUES
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
        (@HistoricalReactJobRequestId, @TenantId, N'TP-HIST-0001', N'React Portal Engineer', N'Historical frontend role focused on React, Angular migration support, Azure-hosted customer portals, and SQL-backed reporting pages.', N'Relia', @EngineeringDepartmentId, @LahoreLocationId, N'FullTime', CAST(4.0 AS DECIMAL(4,1)), CAST(7.0 AS DECIMAL(4,1)), N'Medium', 1, 1, N'Closed', N'Unpublished', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @RecruiterUserId, N'CLOSED', DATEADD(DAY, -180, @Now)),
        (@HistoricalDotNetJobRequestId, @TenantId, N'TP-HIST-0002', N'Angular .NET Product Engineer', N'Historical full-stack role requiring Angular, React familiarity, .NET API collaboration, SQL Server debugging, and delivery with product teams.', N'Enterprise Client', @EngineeringDepartmentId, @RemoteLocationId, N'FullTime', CAST(3.0 AS DECIMAL(4,1)), CAST(6.0 AS DECIMAL(4,1)), N'Medium', 1, 0, N'Closed', N'Unpublished', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @RecruiterUserId, N'CLOSED', DATEADD(DAY, -120, @Now)),
        (@HistoricalQaJobRequestId, @TenantId, N'TP-HIST-0003', N'QA Automation Engineer', N'Historical quality engineering role focused on regression automation, API test coverage, and release readiness.', N'Internal Platform', @QaDepartmentId, @KarachiLocationId, N'FullTime', CAST(4.0 AS DECIMAL(4,1)), CAST(7.0 AS DECIMAL(4,1)), N'Low', 1, 1, N'Closed', N'Unpublished', @HiringManagerUserId, CAST(NULL AS UNIQUEIDENTIFIER), @RecruiterUserId, N'CLOSED', DATEADD(DAY, -90, @Now))
    ) AS source (JobRequestId, TenantId, RequestCode, Title, Description, ClientName, DepartmentId, LocationId, EmploymentType, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, Status, PublishStatus, HiringManagerUserId, HiringManagerGroupId, CreatedByUserId, CurrentStageKey, PublishedAtUtc)
    ON target.JobRequestId = source.JobRequestId
    WHEN MATCHED THEN UPDATE SET Title = source.Title, Description = source.Description, ClientName = source.ClientName, DepartmentId = source.DepartmentId, LocationId = source.LocationId, EmploymentType = source.EmploymentType, ExperienceMinYears = source.ExperienceMinYears, ExperienceMaxYears = source.ExperienceMaxYears, Priority = source.Priority, RequiredPositions = source.RequiredPositions, FulfilledPositions = source.FulfilledPositions, Status = source.Status, PublishStatus = source.PublishStatus, HiringManagerUserId = source.HiringManagerUserId, CreatedByUserId = source.CreatedByUserId, CurrentStageKey = source.CurrentStageKey, PublishedAtUtc = source.PublishedAtUtc, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN INSERT (JobRequestId, TenantId, RequestCode, Title, Description, ClientName, DepartmentId, LocationId, EmploymentType, ExperienceMinYears, ExperienceMaxYears, Priority, RequiredPositions, FulfilledPositions, Status, PublishStatus, HiringManagerUserId, HiringManagerGroupId, CreatedByUserId, CurrentStageKey, PublishedAtUtc, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.JobRequestId, source.TenantId, source.RequestCode, source.Title, source.Description, source.ClientName, source.DepartmentId, source.LocationId, source.EmploymentType, source.ExperienceMinYears, source.ExperienceMaxYears, source.Priority, source.RequiredPositions, source.FulfilledPositions, source.Status, source.PublishStatus, source.HiringManagerUserId, source.HiringManagerGroupId, source.CreatedByUserId, source.CurrentStageKey, source.PublishedAtUtc, @Now, @Now);

    MERGE dbo.JobApplications AS target
    USING (VALUES
        (@ReactHistoricalApplicationId, @TenantId, @HistoricalReactJobRequestId, @ReactCandidateId, @ReferralSourceLabelId, N'Referral', N'Rejected', 1, CAST(0 AS BIT), CAST(0 AS BIT), DATEADD(DAY, -170, @Now), DATEADD(DAY, -170, @Now), DATEADD(DAY, -150, @Now), N'Client selected a local full-stack profile; interviewer feedback stayed positive.'),
        (@AngularHistoricalApplicationId, @TenantId, @HistoricalDotNetJobRequestId, @AngularCandidateId, @LinkedInSourceLabelId, N'LinkedIn', N'OfferDeclined', 1, CAST(0 AS BIT), CAST(0 AS BIT), DATEADD(DAY, -115, @Now), DATEADD(DAY, -115, @Now), DATEADD(DAY, -95, @Now), N'Offer declined due to notice period and compensation timing; feedback recommended keeping warm.'),
        (@HiredHistoricalApplicationId, @TenantId, @HistoricalQaJobRequestId, @HiredCandidateId, @ReferralSourceLabelId, N'Referral', N'Hired', 1, CAST(0 AS BIT), CAST(0 AS BIT), DATEADD(DAY, -85, @Now), DATEADD(DAY, -85, @Now), DATEADD(DAY, -60, @Now), N'Hired on a historical QA role and excluded from rediscovery.')
    ) AS source (JobApplicationId, TenantId, JobRequestId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason)
    ON target.JobApplicationId = source.JobApplicationId
    WHEN MATCHED THEN UPDATE SET CurrentStatus = source.CurrentStatus, IsActive = source.IsActive, FinalDecisionAtUtc = source.FinalDecisionAtUtc, FinalDecisionReason = source.FinalDecisionReason, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN INSERT (JobApplicationId, TenantId, JobRequestId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.JobApplicationId, source.TenantId, source.JobRequestId, source.CandidateId, source.CandidateSourceLabelId, source.SourceLabel, source.CurrentStatus, source.ApplicationVersion, source.IsActive, source.IsInvited, source.ConfirmedAtUtc, source.AppliedAtUtc, source.FinalDecisionAtUtc, source.FinalDecisionReason, @Now, @Now);

    MERGE dbo.Interviews AS target
    USING (VALUES
        (@ReactHistoricalInterviewId, @TenantId, @ReactHistoricalApplicationId, CAST(NULL AS UNIQUEIDENTIFIER), @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -160, @Now), 60, N'Historical technical interview', CAST(NULL AS NVARCHAR(300)), N'Completed'),
        (@AngularHistoricalInterviewId, @TenantId, @AngularHistoricalApplicationId, CAST(NULL AS UNIQUEIDENTIFIER), @InterviewerUserId, @RecruiterUserId, DATEADD(DAY, -105, @Now), 60, N'Historical technical interview', CAST(NULL AS NVARCHAR(300)), N'Completed')
    ) AS source (InterviewId, TenantId, JobApplicationId, JobRequestInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, MeetingLink, LocationText, Status)
    ON target.InterviewId = source.InterviewId
    WHEN MATCHED THEN UPDATE SET Status = source.Status, StartsAtUtc = source.StartsAtUtc, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN INSERT (InterviewId, TenantId, JobApplicationId, JobRequestInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, MeetingLink, LocationText, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.InterviewId, source.TenantId, source.JobApplicationId, source.JobRequestInterviewRoundId, source.InterviewerUserId, source.ScheduledByUserId, source.StartsAtUtc, source.DurationMinutes, source.MeetingLink, source.LocationText, source.Status, @Now, @Now);

    MERGE dbo.InterviewFeedback AS target
    USING (VALUES
        (@ReactHistoricalFeedbackId, @TenantId, @ReactHistoricalInterviewId, @InterviewerUserId, 4, 4, 5, N'Proceed', N'Strong React and portal delivery experience. Good Azure exposure and clear communication; would need a short ramp-up on backend depth.', CAST(1 AS BIT), DATEADD(DAY, -159, @Now)),
        (@AngularHistoricalFeedbackId, @TenantId, @AngularHistoricalInterviewId, @InterviewerUserId, 4, 3, 4, N'Proceed', N'Good Angular product delivery and can support React tasks. Needs validation on SQL Server depth, but previous interviewers marked the candidate as a warm future-fit profile.', CAST(1 AS BIT), DATEADD(DAY, -104, @Now))
    ) AS source (InterviewFeedbackId, TenantId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText, IsSubmitted, SubmittedAtUtc)
    ON target.InterviewFeedbackId = source.InterviewFeedbackId
    WHEN MATCHED THEN UPDATE SET TechnicalScore = source.TechnicalScore, CommunicationScore = source.CommunicationScore, CultureScore = source.CultureScore, Recommendation = source.Recommendation, FeedbackText = source.FeedbackText, IsSubmitted = source.IsSubmitted, SubmittedAtUtc = source.SubmittedAtUtc, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN INSERT (InterviewFeedbackId, TenantId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText, IsSubmitted, SubmittedAtUtc, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.InterviewFeedbackId, source.TenantId, source.InterviewId, source.SubmittedByUserId, source.TechnicalScore, source.CommunicationScore, source.CultureScore, source.Recommendation, source.FeedbackText, source.IsSubmitted, source.SubmittedAtUtc, @Now, @Now);
END;
GO
