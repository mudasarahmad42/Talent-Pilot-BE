SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @DemoPasswordHash NVARCHAR(500) = N'$2a$10$394j2/GNOR2jpagThC4RWOCkDm2HrM4Mb5nCBrkW3D5OTyQKsH4Nu';

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

DECLARE @SystemAdminRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222200';
DECLARE @TenantAdminRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222201';
DECLARE @PresalesRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222202';
DECLARE @PmoRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222203';
DECLARE @RecruiterRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222204';
DECLARE @InterviewerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222205';
DECLARE @HiringManagerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222206';
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222207';
DECLARE @CandidateRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222208';
DECLARE @HodRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222209';

DECLARE @TenantAdminUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333301';
DECLARE @PresalesUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333302';
DECLARE @PmoUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333303';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @CandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333307';
DECLARE @HodUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333311';

DECLARE @PmoEngineeringGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444401';
DECLARE @PmoQaGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444404';
DECLARE @PmoDevOpsGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444405';
DECLARE @RecruitingDeliveryGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444402';
DECLARE @InterviewPanelEngineeringGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444403';

DECLARE @PresalesRequestSubmittedEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555501';
DECLARE @PmoEmployeeReferredEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555502';
DECLARE @PmoForwardedToRecruitingEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555503';
DECLARE @RecruiterAssignedInterviewersEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555504';
DECLARE @InterviewFeedbackSubmittedEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555505';
DECLARE @CandidateStageChangedEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555506';
DECLARE @HiringManagerReviewReadyEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555507';
DECLARE @RealtimeNotificationEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555508';
DECLARE @PresalesEmployeeReferralAcceptedEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555509';
DECLARE @PresalesEmployeeReferralRejectedEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555510';
DECLARE @InterviewScheduledEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555512';
DECLARE @OfferPresentationMeetingScheduledEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555513';

MERGE dbo.Tenants AS target
USING (VALUES
    (@TenantId, N'TKXEL', N'tkxel', N'tkxel.com', N'talent-admin@tkxel.com', N'Asia/Karachi', 'PKR', N'Active', CAST(1 AS BIT))
) AS source (TenantId, DisplayName, Slug, Domain, AdminContactEmail, DefaultTimezoneId, DefaultCurrencyCode, Status, SetupComplete)
ON target.TenantId = source.TenantId
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = source.DisplayName,
        Slug = source.Slug,
        Domain = source.Domain,
        AdminContactEmail = source.AdminContactEmail,
        DefaultTimezoneId = source.DefaultTimezoneId,
        DefaultCurrencyCode = source.DefaultCurrencyCode,
        Status = source.Status,
        SetupComplete = source.SetupComplete,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (TenantId, DisplayName, Slug, Domain, AdminContactEmail, DefaultTimezoneId, DefaultCurrencyCode, Status, SetupComplete, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.TenantId, source.DisplayName, source.Slug, source.Domain, source.AdminContactEmail, source.DefaultTimezoneId, source.DefaultCurrencyCode, source.Status, source.SetupComplete, @Now, @Now);

MERGE dbo.TenantRecruitmentSettings AS target
USING (VALUES
    (@TenantId, N'TKXEL Careers', N'75-C/II, Gulberg III', N'Lahore', N'Pakistan', N'hr@tkxel.com', N'+92 42 111 859 351', N'#2563EB', CAST(1 AS BIT), N'DOCX', CAST(1 AS BIT), 7, 90, N'Resend')
) AS source (TenantId, CareerDisplayName, CompanyAddress, CompanyCity, CompanyCountry, OfficialEmail, OfficialPhone, PrimaryColorHex, CandidateLoginRequired, CandidateCvFormat, PublicJobsEnabled, InviteExpiryDays, ReapplyCooldownDays, NotificationEmailProvider)
ON target.TenantId = source.TenantId
WHEN MATCHED THEN
    UPDATE SET
        CareerDisplayName = source.CareerDisplayName,
        CompanyAddress = source.CompanyAddress,
        CompanyCity = source.CompanyCity,
        CompanyCountry = source.CompanyCountry,
        OfficialEmail = source.OfficialEmail,
        OfficialPhone = source.OfficialPhone,
        PrimaryColorHex = source.PrimaryColorHex,
        CandidateLoginRequired = source.CandidateLoginRequired,
        CandidateCvFormat = source.CandidateCvFormat,
        PublicJobsEnabled = source.PublicJobsEnabled,
        InviteExpiryDays = source.InviteExpiryDays,
        ReapplyCooldownDays = source.ReapplyCooldownDays,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (TenantId, CareerDisplayName, CompanyAddress, CompanyCity, CompanyCountry, OfficialEmail, OfficialPhone, PrimaryColorHex, CandidateLoginRequired, CandidateCvFormat, PublicJobsEnabled, InviteExpiryDays, ReapplyCooldownDays, NotificationEmailProvider, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.TenantId, source.CareerDisplayName, source.CompanyAddress, source.CompanyCity, source.CompanyCountry, source.OfficialEmail, source.OfficialPhone, source.PrimaryColorHex, source.CandidateLoginRequired, source.CandidateCvFormat, source.PublicJobsEnabled, source.InviteExpiryDays, source.ReapplyCooldownDays, source.NotificationEmailProvider, @Now, @Now);

MERGE dbo.Roles AS target
USING (VALUES
    (@SystemAdminRoleId, CAST(NULL AS UNIQUEIDENTIFIER), N'SystemAdmin', N'System Admin', N'System', N'Platform', 1, CAST(1 AS BIT), N'Active'),
    (@TenantAdminRoleId, @TenantId, N'TenantAdmin', N'Tenant Admin', N'Tenant', N'Tenant', 1, CAST(0 AS BIT), N'Active'),
    (@PresalesRoleId, @TenantId, N'Presales', N'Presales', N'Tenant', N'Tenant', 20, CAST(0 AS BIT), N'Active'),
    (@PmoRoleId, @TenantId, N'PMO', N'PMO / Resource Manager', N'Tenant', N'Tenant', 10, CAST(0 AS BIT), N'Active'),
    (@RecruiterRoleId, @TenantId, N'Recruiter', N'Recruiter / HR', N'Tenant', N'Tenant', 30, CAST(0 AS BIT), N'Active'),
    (@InterviewerRoleId, @TenantId, N'Interviewer', N'Interviewer', N'Tenant', N'Tenant', 50, CAST(0 AS BIT), N'Active'),
    (@HiringManagerRoleId, @TenantId, N'HiringManager', N'Hiring Manager', N'Tenant', N'Tenant', 40, CAST(0 AS BIT), N'Active'),
    (@HodRoleId, @TenantId, N'HOD', N'HOD / Department Head', N'Tenant', N'Tenant', 45, CAST(0 AS BIT), N'Active'),
    (@EmployeeRoleId, @TenantId, N'Employee', N'Employee', N'Tenant', N'Tenant', 90, CAST(0 AS BIT), N'Active'),
    (@CandidateRoleId, @TenantId, N'Candidate', N'Candidate', N'Tenant', N'Tenant', 100, CAST(0 AS BIT), N'Active')
) AS source (RoleId, TenantId, Code, Name, Type, Scope, Priority, IsProtected, Status)
ON target.RoleId = source.RoleId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        Code = source.Code,
        Name = source.Name,
        Type = source.Type,
        Scope = source.Scope,
        Priority = source.Priority,
        IsProtected = source.IsProtected,
        Status = source.Status,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (RoleId, TenantId, Code, Name, Type, Scope, Priority, IsProtected, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.RoleId, source.TenantId, source.Code, source.Name, source.Type, source.Scope, source.Priority, source.IsProtected, source.Status, @Now, @Now);

MERGE dbo.Permissions AS target
USING (VALUES
    (N'access.admin.manage', N'Manage Admin Center', N'Admin Center', N'Open and manage tenant administration settings.', N'Active'),
    (N'access.users.manage', N'Manage Users', N'Access Control', N'Create, update, invite, disable, and audit internal users.', N'Active'),
    (N'access.roles.manage', N'Manage Roles', N'Access Control', N'Create tenant roles, assign permissions, and configure role policies.', N'Active'),
    (N'audit.logs.view', N'View Audit Logs', N'Governance', N'View tenant-scoped audit history.', N'Active'),
    (N'tenant.profile.manage', N'Manage Tenant Profile', N'Admin Center', N'Update tenant profile, localization, branding, and career defaults.', N'Active'),
    (N'notifications.manage', N'Manage Notifications', N'Admin Center', N'Update notification event status and email templates.', N'Active'),
    (N'ai.settings.view', N'View AI Runtime', N'AI', N'View configured AI runtime and enabled agent metadata.', N'Active'),
    (N'job.requests.view', N'View Job Requests', N'Recruitment', N'View resource and job requests.', N'Active'),
    (N'job.requests.create', N'Create Job Requests', N'Recruitment', N'Create a resource or hiring request.', N'Active'),
    (N'workflow.assignments.claim', N'Claim Workflow Tasks', N'Workflow', N'Claim and act on workflow baton assignments.', N'Active'),
    (N'bench.matches.view', N'View Bench Matches', N'Recruitment', N'View benched employee recommendations for PMO matching.', N'Active'),
    (N'candidates.manage', N'Manage Candidates', N'Recruitment', N'Manage candidate records, invitations, and applications.', N'Active'),
    (N'interviews.manage', N'Manage Interviews', N'Recruitment', N'Schedule interviews and submit interview feedback.', N'Active'),
    (N'hiring.decisions.manage', N'Record Hiring Decisions', N'Recruitment', N'Review final candidate context and record human hiring decisions.', N'Active')
) AS source (PermissionId, DisplayName, GroupName, Description, Status)
ON target.PermissionId = source.PermissionId
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = source.DisplayName,
        GroupName = source.GroupName,
        Description = source.Description,
        Status = source.Status,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (PermissionId, DisplayName, GroupName, Description, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.PermissionId, source.DisplayName, source.GroupName, source.Description, source.Status, @Now, @Now);

MERGE dbo.AppUsers AS target
USING (VALUES
    (@TenantAdminUserId, @TenantId, N'Mudasar Ahmad', N'admin@tkxel.com', N'admin@tkxel.com', N'MA', N'Active', DATEADD(DAY, -1, @Now)),
    (@PresalesUserId, @TenantId, N'Ahmed Raza', N'ai-presales@8pkk57.onmicrosoft.com', N'ai-presales@8pkk57.onmicrosoft.com', N'AR', N'Active', NULL),
    (@PmoUserId, @TenantId, N'Ali Khan', N'ai-pmo@8pkk57.onmicrosoft.com', N'ai-pmo@8pkk57.onmicrosoft.com', N'AK', N'Active', NULL),
    (@RecruiterUserId, @TenantId, N'Sara Malik', N'ai-recruiter@8pkk57.onmicrosoft.com', N'ai-recruiter@8pkk57.onmicrosoft.com', N'SM', N'Active', NULL),
    (@InterviewerUserId, @TenantId, N'Bilal Hussain', N'ai-interviewer@8pkk57.onmicrosoft.com', N'ai-interviewer@8pkk57.onmicrosoft.com', N'BH', N'Active', NULL),
    (@HiringManagerUserId, @TenantId, N'Fatima Noor', N'ai-hiring.manager@8pkk57.onmicrosoft.com', N'ai-hiring.manager@8pkk57.onmicrosoft.com', N'FN', N'Active', NULL),
    (@HodUserId, @TenantId, N'Zara Siddiqui', N'ai-hod.engineering@8pkk57.onmicrosoft.com', N'ai-hod.engineering@8pkk57.onmicrosoft.com', N'ZS', N'Active', NULL),
    (@CandidateUserId, @TenantId, N'Ayesha Khan', N'ai-candidate@8pkk57.onmicrosoft.com', N'ai-candidate@8pkk57.onmicrosoft.com', N'AK', N'Active', NULL)
) AS source (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, LastActiveAtUtc)
ON target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        DisplayName = source.DisplayName,
        Email = source.Email,
        EmailNormalized = UPPER(source.EmailNormalized),
        Initials = source.Initials,
        AccountStatus = source.AccountStatus,
        LastActiveAtUtc = COALESCE(target.LastActiveAtUtc, source.LastActiveAtUtc),
        UpdatedAtUtc = @Now,
        DeletedAtUtc = NULL
WHEN NOT MATCHED THEN
    INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, LastActiveAtUtc, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.UserId, source.TenantId, source.DisplayName, source.Email, UPPER(source.EmailNormalized), source.Initials, source.AccountStatus, source.LastActiveAtUtc, @Now, @Now);

MERGE dbo.UserCredentials AS target
USING (VALUES
    ('77777777-7777-7777-7777-777777777301', @TenantId, @TenantAdminUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777302', @TenantId, @PresalesUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777303', @TenantId, @PmoUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777304', @TenantId, @RecruiterUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777305', @TenantId, @InterviewerUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777306', @TenantId, @HiringManagerUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777311', @TenantId, @HodUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777307', @TenantId, @CandidateUserId, @DemoPasswordHash)
) AS source (UserCredentialId, TenantId, UserId, PasswordHash)
ON target.UserCredentialId = source.UserCredentialId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        UserId = source.UserId,
        PasswordHash = source.PasswordHash,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (UserCredentialId, TenantId, UserId, PasswordHash, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.UserCredentialId, source.TenantId, source.UserId, source.PasswordHash, @Now, @Now);

MERGE dbo.UserRoles AS target
USING (VALUES
    (@TenantId, @TenantAdminUserId, @TenantAdminRoleId, @TenantAdminUserId),
    (@TenantId, @PresalesUserId, @PresalesRoleId, @TenantAdminUserId),
    (@TenantId, @PmoUserId, @PmoRoleId, @TenantAdminUserId),
    (@TenantId, @RecruiterUserId, @RecruiterRoleId, @TenantAdminUserId),
    (@TenantId, @InterviewerUserId, @InterviewerRoleId, @TenantAdminUserId),
    (@TenantId, @HiringManagerUserId, @HiringManagerRoleId, @TenantAdminUserId),
    (@TenantId, @HodUserId, @HodRoleId, @TenantAdminUserId),
    (@TenantId, @CandidateUserId, @CandidateRoleId, @TenantAdminUserId)
) AS source (TenantId, UserId, RoleId, AssignedByUserId)
ON target.TenantId = source.TenantId AND target.UserId = source.UserId AND target.RoleId = source.RoleId
WHEN NOT MATCHED THEN
    INSERT (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
    VALUES (source.TenantId, source.UserId, source.RoleId, source.AssignedByUserId, @Now);

MERGE dbo.RolePermissions AS target
USING (
    SELECT roleSource.RoleId, permissionSource.PermissionId
    FROM (VALUES
        (@TenantAdminRoleId, N'access.admin.manage'), (@TenantAdminRoleId, N'access.users.manage'), (@TenantAdminRoleId, N'access.roles.manage'),
        (@TenantAdminRoleId, N'audit.logs.view'), (@TenantAdminRoleId, N'tenant.profile.manage'), (@TenantAdminRoleId, N'notifications.manage'),
        (@TenantAdminRoleId, N'ai.settings.view'), (@TenantAdminRoleId, N'job.requests.view'), (@TenantAdminRoleId, N'job.requests.create'),
        (@TenantAdminRoleId, N'workflow.assignments.claim'), (@TenantAdminRoleId, N'bench.matches.view'), (@TenantAdminRoleId, N'candidates.manage'),
        (@TenantAdminRoleId, N'interviews.manage'), (@TenantAdminRoleId, N'hiring.decisions.manage'),
        (@SystemAdminRoleId, N'access.admin.manage'), (@SystemAdminRoleId, N'access.users.manage'), (@SystemAdminRoleId, N'access.roles.manage'),
        (@SystemAdminRoleId, N'audit.logs.view'), (@SystemAdminRoleId, N'tenant.profile.manage'), (@SystemAdminRoleId, N'notifications.manage'),
        (@SystemAdminRoleId, N'ai.settings.view'), (@SystemAdminRoleId, N'job.requests.view'), (@SystemAdminRoleId, N'job.requests.create'),
        (@SystemAdminRoleId, N'workflow.assignments.claim'), (@SystemAdminRoleId, N'bench.matches.view'), (@SystemAdminRoleId, N'candidates.manage'),
        (@SystemAdminRoleId, N'interviews.manage'), (@SystemAdminRoleId, N'hiring.decisions.manage'),
        (@PresalesRoleId, N'job.requests.view'), (@PresalesRoleId, N'job.requests.create'),
        (@PmoRoleId, N'job.requests.view'), (@PmoRoleId, N'job.requests.create'), (@PmoRoleId, N'workflow.assignments.claim'), (@PmoRoleId, N'bench.matches.view'),
        (@RecruiterRoleId, N'job.requests.view'), (@RecruiterRoleId, N'workflow.assignments.claim'), (@RecruiterRoleId, N'candidates.manage'), (@RecruiterRoleId, N'interviews.manage'),
        (@InterviewerRoleId, N'workflow.assignments.claim'), (@InterviewerRoleId, N'interviews.manage'),
        (@HodRoleId, N'workflow.assignments.claim'), (@HodRoleId, N'interviews.manage'),
        (@HiringManagerRoleId, N'job.requests.view'), (@HiringManagerRoleId, N'workflow.assignments.claim'), (@HiringManagerRoleId, N'hiring.decisions.manage')
    ) AS roleSource (RoleId, PermissionId)
    INNER JOIN dbo.Permissions AS permissionSource ON permissionSource.PermissionId = roleSource.PermissionId
) AS source (RoleId, PermissionId)
ON target.RoleId = source.RoleId AND target.PermissionId = source.PermissionId
WHEN NOT MATCHED THEN
    INSERT (RoleId, PermissionId, CreatedAtUtc)
    VALUES (source.RoleId, source.PermissionId, @Now);

MERGE dbo.Groups AS target
USING (VALUES
    (@PmoEngineeringGroupId, @TenantId, N'PMO - Engineering', N'WorkflowRouting', @PmoUserId, N'Active'),
    (@PmoQaGroupId, @TenantId, N'PMO - QA', N'WorkflowRouting', @PmoUserId, N'Active'),
    (@PmoDevOpsGroupId, @TenantId, N'PMO - DevOps', N'WorkflowRouting', @PmoUserId, N'Active'),
    (@RecruitingDeliveryGroupId, @TenantId, N'Recruiting - Delivery', N'WorkflowRouting', @RecruiterUserId, N'Active'),
    (@InterviewPanelEngineeringGroupId, @TenantId, N'Interview Panel - Engineering', N'WorkflowRouting', @InterviewerUserId, N'Active')
) AS source (GroupId, TenantId, Name, Purpose, DefaultOwnerUserId, Status)
ON target.GroupId = source.GroupId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        Name = source.Name,
        Purpose = source.Purpose,
        DefaultOwnerUserId = source.DefaultOwnerUserId,
        Status = source.Status,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (GroupId, TenantId, Name, Purpose, DefaultOwnerUserId, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.GroupId, source.TenantId, source.Name, source.Purpose, source.DefaultOwnerUserId, source.Status, @Now, @Now);

MERGE dbo.GroupMembers AS target
USING (VALUES
    (@TenantId, @PmoEngineeringGroupId, @PmoUserId, CAST(1 AS BIT)),
    (@TenantId, @PmoQaGroupId, @PmoUserId, CAST(1 AS BIT)),
    (@TenantId, @PmoDevOpsGroupId, @PmoUserId, CAST(1 AS BIT)),
    (@TenantId, @RecruitingDeliveryGroupId, @RecruiterUserId, CAST(1 AS BIT)),
    (@TenantId, @InterviewPanelEngineeringGroupId, @InterviewerUserId, CAST(1 AS BIT)),
    (@TenantId, @InterviewPanelEngineeringGroupId, @HodUserId, CAST(0 AS BIT)),
    (@TenantId, @InterviewPanelEngineeringGroupId, @HiringManagerUserId, CAST(0 AS BIT))
) AS source (TenantId, GroupId, UserId, IsDefaultAssignee)
ON target.TenantId = source.TenantId AND target.GroupId = source.GroupId AND target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET IsDefaultAssignee = source.IsDefaultAssignee
WHEN NOT MATCHED THEN
    INSERT (TenantId, GroupId, UserId, IsDefaultAssignee, CreatedAtUtc)
    VALUES (source.TenantId, source.GroupId, source.UserId, source.IsDefaultAssignee, @Now);

MERGE dbo.TenantAccessPolicies AS target
USING (VALUES
    ('88888888-8888-8888-8888-888888888801', @TenantId, N'MergeAllAssignedRoles', @PmoRoleId, N'TenantAdmins', @TenantAdminUserId)
) AS source (TenantAccessPolicyId, TenantId, PermissionResolutionMode, BenchVisibilityRoleId, GroupFallbackMode, UpdatedByUserId)
ON target.TenantId = source.TenantId
WHEN MATCHED THEN
    UPDATE SET
        PermissionResolutionMode = source.PermissionResolutionMode,
        BenchVisibilityRoleId = source.BenchVisibilityRoleId,
        GroupFallbackMode = source.GroupFallbackMode,
        UpdatedByUserId = source.UpdatedByUserId,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (TenantAccessPolicyId, TenantId, PermissionResolutionMode, BenchVisibilityRoleId, GroupFallbackMode, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.TenantAccessPolicyId, source.TenantId, source.PermissionResolutionMode, source.BenchVisibilityRoleId, source.GroupFallbackMode, source.UpdatedByUserId, @Now, @Now);

MERGE dbo.NotificationEvents AS target
USING (VALUES
    (@PresalesRequestSubmittedEventId, @TenantId, N'PRESALES_REQUEST_SUBMITTED', N'Presales request submitted', N'DepartmentIntakeRoute', N'Active'),
    (@PmoEmployeeReferredEventId, @TenantId, N'PMO_EMPLOYEE_REFERRED', N'PMO referred employee', N'User:PresalesOwner', N'Active'),
    (@PmoForwardedToRecruitingEventId, @TenantId, N'PMO_FORWARDED_TO_RECRUITING', N'PMO forwarded to recruiting', N'Group:Recruiting', N'Active'),
    (@PresalesEmployeeReferralAcceptedEventId, @TenantId, N'PRESALES_EMPLOYEE_REFERRAL_ACCEPTED', N'Presales accepted employee referral', N'User:PMOReferralOwner', N'Active'),
    (@PresalesEmployeeReferralRejectedEventId, @TenantId, N'PRESALES_EMPLOYEE_REFERRAL_REJECTED', N'Presales rejected employee referral', N'User:PMOReferralOwner', N'Active'),
    (@RecruiterAssignedInterviewersEventId, @TenantId, N'RECRUITER_ASSIGNED_INTERVIEWERS', N'Recruiter assigned interviewers', N'User:Interviewer', N'Active'),
    (@InterviewScheduledEventId, @TenantId, N'INTERVIEW_SCHEDULED', N'Interview scheduled', N'User:InterviewParticipants', N'Active'),
    (@InterviewFeedbackSubmittedEventId, @TenantId, N'INTERVIEW_FEEDBACK_SUBMITTED', N'Interview feedback submitted', N'User:Recruiter', N'Active'),
    (@CandidateStageChangedEventId, @TenantId, N'CANDIDATE_STAGE_CHANGED', N'Candidate stage changed', N'User:CandidateOrOwner', N'Active'),
    (@HiringManagerReviewReadyEventId, @TenantId, N'HIRING_MANAGER_REVIEW_READY', N'Hiring manager review ready', N'User:HiringManager', N'Active'),
    (@OfferPresentationMeetingScheduledEventId, @TenantId, N'OFFER_PRESENTATION_MEETING_SCHEDULED', N'Offer presentation meeting scheduled', N'User:Candidate', N'Active'),
    (@RealtimeNotificationEventId, @TenantId, N'REALTIME_NOTIFICATION', N'Realtime notification', N'Realtime', N'Active')
) AS source (NotificationEventId, TenantId, EventCode, Name, DefaultRecipientType, Status)
ON target.TenantId = source.TenantId AND target.EventCode = source.EventCode
WHEN MATCHED THEN
    UPDATE SET
        Name = source.Name,
        DefaultRecipientType = source.DefaultRecipientType,
        Status = source.Status,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (NotificationEventId, TenantId, EventCode, Name, DefaultRecipientType, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.NotificationEventId, source.TenantId, source.EventCode, source.Name, source.DefaultRecipientType, source.Status, @Now, @Now);

MERGE dbo.NotificationTemplates AS target
USING
(
    SELECT
        source.NotificationTemplateId,
        source.TenantId,
        event.NotificationEventId,
        source.Name,
        source.Recipient,
        source.Subject,
        source.Body,
        source.AllowedVariablesJson,
        source.Status,
        source.UpdatedByUserId
    FROM
    (
        VALUES
            ('66666666-6666-6666-6666-666666666601', @TenantId, N'PRESALES_REQUEST_SUBMITTED', N'PMO intake email', N'Configured department intake recipient', N'New request: {{jobTitle}}', N'{{requesterName}} submitted {{jobTitle}} for PMO review.', N'["jobTitle","requesterName"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666602', @TenantId, N'PMO_EMPLOYEE_REFERRED', N'Employee referral email', N'Presales Owner', N'PMO referred {{employeeName}}', N'{{employeeName}} was referred for {{jobTitle}}. Review the recommendation in Talent Pilot.', N'["employeeName","jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666603', @TenantId, N'PMO_FORWARDED_TO_RECRUITING', N'Recruiting handoff email', N'Recruiting - Delivery', N'Recruiting handoff: {{jobTitle}}', N'PMO forwarded {{jobTitle}} to recruiting after bench review.', N'["jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666608', @TenantId, N'PRESALES_EMPLOYEE_REFERRAL_ACCEPTED', N'Accepted referral email', N'PMO Referral Owner', N'Presales accepted an internal employee for {{jobTitle}}', N'{{requesterName}} accepted {{acceptedCount}} internal employee recommendation(s) and rejected {{rejectedCount}} for {{jobTitle}}.', N'["requesterName","acceptedCount","rejectedCount","jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666609', @TenantId, N'PRESALES_EMPLOYEE_REFERRAL_REJECTED', N'Rejected referral email', N'PMO Referral Owner', N'Presales rejected internal recommendations for {{jobTitle}}', N'{{requesterName}} rejected {{rejectedCount}} internal employee recommendation(s) for {{jobTitle}}. The request has returned to PMO Review.', N'["requesterName","rejectedCount","jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666604', @TenantId, N'RECRUITER_ASSIGNED_INTERVIEWERS', N'Interview assignment email', N'Interviewer', N'Interview assigned: {{candidateName}}', N'You have been assigned to interview {{candidateName}} for {{jobTitle}}.', N'["candidateName","jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666610', @TenantId, N'INTERVIEW_SCHEDULED', N'Interview scheduled email', N'Candidate, Interviewer, Hiring Manager', N'Interview scheduled: {{jobTitle}}', N'Interview scheduling emails are generated by backend code with candidate, interviewer, hiring manager, date, duration, and meeting details.', N'["jobTitle","candidateName","roundName","startsAtUtc","durationMinutes","meetingLink","locationText"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666605', @TenantId, N'INTERVIEW_FEEDBACK_SUBMITTED', N'Feedback received email', N'Recruiter', N'Feedback submitted for {{candidateName}}', N'Interview feedback for {{candidateName}} is ready for recruiter review.', N'["candidateName"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666606', @TenantId, N'CANDIDATE_STAGE_CHANGED', N'Candidate stage email', N'Candidate or Owner', N'Application update: {{stageName}}', N'{{candidateName}} moved to {{stageName}} for {{jobTitle}}.', N'["candidateName","stageName","jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666607', @TenantId, N'HIRING_MANAGER_REVIEW_READY', N'Hiring manager review email', N'Hiring Manager', N'Final review ready: {{candidateName}}', N'{{candidateName}} is ready for final hiring-manager review for {{jobTitle}}.', N'["candidateName","jobTitle"]', N'Active', @TenantAdminUserId),
            ('66666666-6666-6666-6666-666666666613', @TenantId, N'OFFER_PRESENTATION_MEETING_SCHEDULED', N'Offer presentation meeting email', N'Candidate', N'Offer presentation scheduled: {{jobTitle}}', N'Your in-person offer presentation for {{jobTitle}} is scheduled at {{meetingAtUtc}}. Location: {{physicalLocation}}.', N'["jobTitle","meetingAtUtc","physicalLocation"]', N'Active', @TenantAdminUserId)
    ) AS source (NotificationTemplateId, TenantId, EventCode, Name, Recipient, Subject, Body, AllowedVariablesJson, Status, UpdatedByUserId)
    INNER JOIN dbo.NotificationEvents AS event
        ON event.TenantId = source.TenantId
        AND event.EventCode = source.EventCode
) AS source
ON target.TenantId = source.TenantId
   AND target.NotificationEventId = source.NotificationEventId
   AND target.Name = source.Name
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        NotificationEventId = source.NotificationEventId,
        Name = source.Name,
        Recipient = source.Recipient,
        Subject = source.Subject,
        Body = source.Body,
        AllowedVariablesJson = source.AllowedVariablesJson,
        Status = source.Status,
        UpdatedByUserId = source.UpdatedByUserId,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (NotificationTemplateId, TenantId, NotificationEventId, Name, Recipient, Subject, Body, AllowedVariablesJson, Status, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.NotificationTemplateId, source.TenantId, source.NotificationEventId, source.Name, source.Recipient, source.Subject, source.Body, source.AllowedVariablesJson, source.Status, source.UpdatedByUserId, @Now, @Now);

MERGE dbo.AiAgentDefinitions AS target
USING (VALUES
    (N'requirement-parser', N'Requirement Parser', N'Builds the saved requirement profile used for future semantic matching when a Job Request is created.', N'Controlled Job Request intake fields, final saved description, department, location, skills, experience range, positions, and priority.', N'Indexed requirement profile and embedding metadata for downstream agents.', N'Runs after save; it cannot approve, reject, or move workflow stages.', CAST(1 AS BIT)),
    (N'job-description-drafter', N'Job Description Drafter', N'Drafts editable Job Request descriptions from controlled intake fields.', N'Job title, client, department, location, selected tenant skills, experience range, required positions, priority, and hiring manager.', N'Plain-text job description ready for human editing.', N'Human review is required before save; the agent cannot approve, reject, or move workflow stages.', CAST(1 AS BIT)),
    (N'cv-parser', N'CV Parser', N'Prefills recruiter manual sourcing forms from DOCX resumes.', N'DOCX text extracted server-side from the Add Candidate flow.', N'Structured candidate contact, profile, education, experience, and skill evidence for recruiter review.', N'DOCX only for MVP; recruiters review and edit every extracted field before inviting.', CAST(1 AS BIT)),
    (N'bench-matching', N'Bench Matching', N'Ranks eligible internal employees for PMO Review using skill coverage, vector similarity, experience, location, availability, project evidence, and optional Tavily web research capped at 60 requests per day.', N'Claimed PMO Review request, required skills, saved job description embedding, active benched employees, employee skills, employee locations, project assignments, and safe public client/project snippets when available.', N'Ranked employee matches with score, confidence, strengths, gaps, location fit, project evidence, web research status, and fit rationale.', N'PMO decides whether to refer an employee; the agent cannot recommend directly to Presales or move workflow stages.', CAST(1 AS BIT)),
    (N'talent-rediscovery', N'Talent Rediscovery', N'Ranks previous warm candidates before external sourcing using candidate skills, historical applications, interview feedback, outcomes, and vector similarity. No web search is used for candidate data.', N'Claimed Recruiter Sourcing request or draft Job Post, active tenant candidates with useful historical applications, candidate skills, interview feedback, prior outcomes, and candidate profile embeddings.', N'Ranked warm candidates with score, confidence, matched skills, gaps, prior application evidence, interview evidence, and caveats.', N'Recruiters review the ranking manually; the agent cannot contact candidates, move workflow stages, or make hiring decisions.', CAST(1 AS BIT)),
    (N'applicant-ranking', N'Applicant Ranking', N'Ranks current applications for an active job post using candidate profile data, application evidence, uploaded CV/cover-letter context, interview history, and vector similarity. No web search is used.', N'Claimed Recruiter Sourcing job post, current active job-post applications, candidate skills/profile fields, cover letter, uploaded application documents, historical applications, interview feedback, and application/job post embeddings.', N'Ranked current applications with deterministic score, confidence, matched skills, gaps, document evidence, historical outcome evidence, semantic similarity status, and recruiter-facing rationale.', N'Recruiters decide whether to shortlist, schedule, hold, reject, or forward. The agent cannot contact candidates or move workflow stages.', CAST(1 AS BIT)),
    (N'fit-explanation', N'Fit Explanation', N'Explains why an employee or candidate was ranked in Bench Matching or Talent Rediscovery.', N'Recommendation evidence, skills, experience, location, project/application history, interview evidence, and gaps.', N'Readable strengths, gaps, confidence notes, and caveats embedded in the ranking result.', N'Explanation supports human review only and never selects or contacts candidates by itself.', CAST(1 AS BIT)),
    (N'hiring-manager-decision-brief', N'Hiring Manager Decision Brief', N'Summarizes interview feedback and candidate context on Hiring Manager Review.', N'Candidate profile, source details, recruiter notes, job request/post summary, interview statuses, scores, recommendations, and skipped-round reasons.', N'Advisory decision brief shown to the Hiring Manager before offer or final outcome actions.', N'Hiring Manager owns the final decision; the brief cannot generate offers or close requests by itself.', CAST(1 AS BIT))
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
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.AiAgentDefinitionId, source.DisplayName, source.Responsibility, source.InputSummary, source.OutputSummary, source.MvpBoundary, source.Enabled, @Now, @Now);

MERGE dbo.AuditLogs AS target
USING (VALUES
    ('99999999-9999-9999-9999-999999999901', @TenantId, @TenantAdminUserId, N'Mudasar Ahmad', N'database.seed.initial', N'Tenant', @TenantId, N'TKXEL tenant', N'Initial Talent Pilot MVP data was seeded.', N'Admin Center', N'{"seed":"001_seed_initial_data"}')
) AS source (AuditLogId, TenantId, ActorUserId, ActorDisplayName, EventType, EntityType, EntityId, RecordLabel, EventSummary, Area, MetadataJson)
ON target.AuditLogId = source.AuditLogId
WHEN MATCHED THEN
    UPDATE SET
        ActorUserId = source.ActorUserId,
        ActorDisplayName = source.ActorDisplayName,
        EventSummary = source.EventSummary,
        MetadataJson = source.MetadataJson
WHEN NOT MATCHED THEN
    INSERT (AuditLogId, TenantId, OccurredAtUtc, ActorUserId, ActorDisplayName, EventType, EntityType, EntityId, RecordLabel, EventSummary, Area, MetadataJson)
    VALUES (source.AuditLogId, source.TenantId, @Now, source.ActorUserId, source.ActorDisplayName, source.EventType, source.EntityType, source.EntityId, source.RecordLabel, source.EventSummary, source.Area, source.MetadataJson);
GO
