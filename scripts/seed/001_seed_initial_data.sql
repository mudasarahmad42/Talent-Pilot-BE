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
DECLARE @CandidateRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222208';

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
DECLARE @DemoPasswordHash NVARCHAR(500) = N'$2a$11$1rAtSdH5kQ8Md5cxRM5IIefOuJg0ax/EYiaCoXCL443RPe4v5/VJK'; -- password: demo

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
    (@TenantId, N'TKXEL Careers', N'#2563EB', CAST(1 AS BIT), N'DOCX', CAST(1 AS BIT), 7, 90)
) AS source (TenantId, CareerDisplayName, PrimaryColorHex, CandidateLoginRequired, CandidateCvFormat, PublicJobsEnabled, InviteExpiryDays, ReapplyCooldownDays)
ON target.TenantId = source.TenantId
WHEN MATCHED THEN
    UPDATE SET
        CareerDisplayName = source.CareerDisplayName,
        PrimaryColorHex = source.PrimaryColorHex,
        CandidateLoginRequired = source.CandidateLoginRequired,
        CandidateCvFormat = source.CandidateCvFormat,
        PublicJobsEnabled = source.PublicJobsEnabled,
        InviteExpiryDays = source.InviteExpiryDays,
        ReapplyCooldownDays = source.ReapplyCooldownDays,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (TenantId, CareerDisplayName, PrimaryColorHex, CandidateLoginRequired, CandidateCvFormat, PublicJobsEnabled, InviteExpiryDays, ReapplyCooldownDays, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.TenantId, source.CareerDisplayName, source.PrimaryColorHex, source.CandidateLoginRequired, source.CandidateCvFormat, source.PublicJobsEnabled, source.InviteExpiryDays, source.ReapplyCooldownDays, @Now, @Now);

MERGE dbo.Roles AS target
USING (VALUES
    (@TenantAdminRoleId, @TenantId, N'TenantAdmin', N'Tenant Admin', N'System', N'Tenant', 1, CAST(1 AS BIT), N'Active'),
    (@PresalesRoleId, @TenantId, N'Presales', N'Presales', N'System', N'Tenant', 20, CAST(1 AS BIT), N'Active'),
    (@PmoRoleId, @TenantId, N'PMO', N'PMO / Resource Manager', N'System', N'Tenant', 10, CAST(1 AS BIT), N'Active'),
    (@RecruiterRoleId, @TenantId, N'Recruiter', N'Recruiter / HR', N'System', N'Tenant', 30, CAST(1 AS BIT), N'Active'),
    (@InterviewerRoleId, @TenantId, N'Interviewer', N'Interviewer', N'System', N'Tenant', 50, CAST(1 AS BIT), N'Active'),
    (@HiringManagerRoleId, @TenantId, N'HiringManager', N'Hiring Manager', N'System', N'Tenant', 40, CAST(1 AS BIT), N'Active'),
    (@EmployeeRoleId, @TenantId, N'Employee', N'Employee', N'System', N'Tenant', 90, CAST(1 AS BIT), N'Active'),
    (@CandidateRoleId, @TenantId, N'Candidate', N'Candidate', N'System', N'Portal', 100, CAST(1 AS BIT), N'Active')
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
    (@TenantAdminUserId, @TenantId, N'Mudasar Ahmad', N'admin@tkxel.com', N'ADMIN@TKXEL.COM', N'MA', N'Active', DATEADD(DAY, -1, @Now)),
    (@PresalesUserId, @TenantId, N'Ahmed Raza', N'presales@tkxel.com', N'PRESALES@TKXEL.COM', N'AR', N'Active', NULL),
    (@PmoUserId, @TenantId, N'Ali Khan', N'pmo@tkxel.com', N'PMO@TKXEL.COM', N'AK', N'Active', NULL),
    (@RecruiterUserId, @TenantId, N'Sara Malik', N'recruiter@tkxel.com', N'RECRUITER@TKXEL.COM', N'SM', N'Active', NULL),
    (@InterviewerUserId, @TenantId, N'Bilal Hussain', N'interviewer@tkxel.com', N'INTERVIEWER@TKXEL.COM', N'BH', N'Active', NULL),
    (@HiringManagerUserId, @TenantId, N'Fatima Noor', N'hiring.manager@tkxel.com', N'HIRING.MANAGER@TKXEL.COM', N'FN', N'Active', NULL),
    (@CandidateUserId, @TenantId, N'Ayesha Khan', N'ayesha.khan@example.com', N'AYESHA.KHAN@EXAMPLE.COM', N'AK', N'Active', NULL)
) AS source (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, LastActiveAtUtc)
ON target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        DisplayName = source.DisplayName,
        Email = source.Email,
        EmailNormalized = source.EmailNormalized,
        Initials = source.Initials,
        AccountStatus = source.AccountStatus,
        LastActiveAtUtc = COALESCE(target.LastActiveAtUtc, source.LastActiveAtUtc),
        UpdatedAtUtc = @Now,
        DeletedAtUtc = NULL
WHEN NOT MATCHED THEN
    INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, LastActiveAtUtc, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.UserId, source.TenantId, source.DisplayName, source.Email, source.EmailNormalized, source.Initials, source.AccountStatus, source.LastActiveAtUtc, @Now, @Now);

MERGE dbo.UserCredentials AS target
USING (VALUES
    ('77777777-7777-7777-7777-777777777301', @TenantId, @TenantAdminUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777302', @TenantId, @PresalesUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777303', @TenantId, @PmoUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777304', @TenantId, @RecruiterUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777305', @TenantId, @InterviewerUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777306', @TenantId, @HiringManagerUserId, @DemoPasswordHash),
    ('77777777-7777-7777-7777-777777777307', @TenantId, @CandidateUserId, @DemoPasswordHash)
) AS source (UserCredentialId, TenantId, UserId, PasswordHash)
ON target.UserCredentialId = source.UserCredentialId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        UserId = source.UserId,
        PasswordHash = source.PasswordHash,
        PasswordUpdatedAtUtc = COALESCE(target.PasswordUpdatedAtUtc, @Now),
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (UserCredentialId, TenantId, UserId, PasswordHash, PasswordUpdatedAtUtc, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.UserCredentialId, source.TenantId, source.UserId, source.PasswordHash, @Now, @Now, @Now);

MERGE dbo.UserRoles AS target
USING (VALUES
    (@TenantId, @TenantAdminUserId, @TenantAdminRoleId, @TenantAdminUserId),
    (@TenantId, @PresalesUserId, @PresalesRoleId, @TenantAdminUserId),
    (@TenantId, @PmoUserId, @PmoRoleId, @TenantAdminUserId),
    (@TenantId, @RecruiterUserId, @RecruiterRoleId, @TenantAdminUserId),
    (@TenantId, @InterviewerUserId, @InterviewerRoleId, @TenantAdminUserId),
    (@TenantId, @HiringManagerUserId, @HiringManagerRoleId, @TenantAdminUserId),
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
        (@PresalesRoleId, N'job.requests.view'), (@PresalesRoleId, N'job.requests.create'),
        (@PmoRoleId, N'job.requests.view'), (@PmoRoleId, N'workflow.assignments.claim'), (@PmoRoleId, N'bench.matches.view'),
        (@RecruiterRoleId, N'job.requests.view'), (@RecruiterRoleId, N'workflow.assignments.claim'), (@RecruiterRoleId, N'candidates.manage'), (@RecruiterRoleId, N'interviews.manage'),
        (@InterviewerRoleId, N'workflow.assignments.claim'), (@InterviewerRoleId, N'interviews.manage'),
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
    (@PmoGroupId, @TenantId, N'PMO Queue', N'WorkflowRouting', @PmoUserId, N'Active'),
    (@RecruitingGroupId, @TenantId, N'Recruiting Queue', N'WorkflowRouting', @RecruiterUserId, N'Active'),
    (@InterviewPanelGroupId, @TenantId, N'Interview Panel', N'WorkflowRouting', @InterviewerUserId, N'Active')
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
    (@TenantId, @PmoGroupId, @PmoUserId, CAST(1 AS BIT)),
    (@TenantId, @RecruitingGroupId, @RecruiterUserId, CAST(1 AS BIT)),
    (@TenantId, @InterviewPanelGroupId, @InterviewerUserId, CAST(1 AS BIT)),
    (@TenantId, @InterviewPanelGroupId, @HiringManagerUserId, CAST(0 AS BIT))
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
    ('55555555-5555-5555-5555-555555555501', @TenantId, N'PRESALES_REQUEST_SUBMITTED', N'Presales request submitted', N'Group:PMO', N'Active'),
    ('55555555-5555-5555-5555-555555555502', @TenantId, N'PMO_EMPLOYEE_REFERRED', N'PMO referred employee', N'User:PresalesOwner', N'Active'),
    ('55555555-5555-5555-5555-555555555503', @TenantId, N'PMO_FORWARDED_TO_RECRUITING', N'PMO forwarded to recruiting', N'Group:Recruiting', N'Active'),
    ('55555555-5555-5555-5555-555555555504', @TenantId, N'RECRUITER_ASSIGNED_INTERVIEWERS', N'Recruiter assigned interviewers', N'User:Interviewer', N'Active'),
    ('55555555-5555-5555-5555-555555555505', @TenantId, N'INTERVIEW_FEEDBACK_SUBMITTED', N'Interview feedback submitted', N'User:Recruiter', N'Active'),
    ('55555555-5555-5555-5555-555555555506', @TenantId, N'CANDIDATE_STAGE_CHANGED', N'Candidate stage changed', N'User:CandidateOrOwner', N'Active'),
    ('55555555-5555-5555-5555-555555555507', @TenantId, N'HIRING_MANAGER_REVIEW_READY', N'Hiring manager review ready', N'User:HiringManager', N'Active')
) AS source (NotificationEventId, TenantId, EventCode, Name, DefaultRecipientType, Status)
ON target.NotificationEventId = source.NotificationEventId
WHEN MATCHED THEN
    UPDATE SET
        TenantId = source.TenantId,
        EventCode = source.EventCode,
        Name = source.Name,
        DefaultRecipientType = source.DefaultRecipientType,
        Status = source.Status,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (NotificationEventId, TenantId, EventCode, Name, DefaultRecipientType, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.NotificationEventId, source.TenantId, source.EventCode, source.Name, source.DefaultRecipientType, source.Status, @Now, @Now);

MERGE dbo.NotificationTemplates AS target
USING (VALUES
    ('66666666-6666-6666-6666-666666666601', @TenantId, '55555555-5555-5555-5555-555555555501', N'PMO intake email', N'PMO Queue', N'New request: {{jobTitle}}', N'{{requesterName}} submitted {{jobTitle}} for PMO review.', N'["jobTitle","requesterName"]', N'Active', @TenantAdminUserId),
    ('66666666-6666-6666-6666-666666666602', @TenantId, '55555555-5555-5555-5555-555555555502', N'Employee referral email', N'Presales Owner', N'PMO referred {{employeeName}}', N'{{employeeName}} was referred for {{jobTitle}}. Review the recommendation in Talent Pilot.', N'["employeeName","jobTitle"]', N'Active', @TenantAdminUserId),
    ('66666666-6666-6666-6666-666666666603', @TenantId, '55555555-5555-5555-5555-555555555503', N'Recruiting handoff email', N'Recruiting Queue', N'Recruiting handoff: {{jobTitle}}', N'PMO forwarded {{jobTitle}} to recruiting after bench review.', N'["jobTitle"]', N'Active', @TenantAdminUserId),
    ('66666666-6666-6666-6666-666666666604', @TenantId, '55555555-5555-5555-5555-555555555504', N'Interview assignment email', N'Interviewer', N'Interview assigned: {{candidateName}}', N'You have been assigned to interview {{candidateName}} for {{jobTitle}}.', N'["candidateName","jobTitle"]', N'Active', @TenantAdminUserId),
    ('66666666-6666-6666-6666-666666666605', @TenantId, '55555555-5555-5555-5555-555555555505', N'Feedback received email', N'Recruiter', N'Feedback submitted for {{candidateName}}', N'Interview feedback for {{candidateName}} is ready for recruiter review.', N'["candidateName"]', N'Active', @TenantAdminUserId),
    ('66666666-6666-6666-6666-666666666606', @TenantId, '55555555-5555-5555-5555-555555555506', N'Candidate stage email', N'Candidate or Owner', N'Application update: {{stageName}}', N'{{candidateName}} moved to {{stageName}} for {{jobTitle}}.', N'["candidateName","stageName","jobTitle"]', N'Active', @TenantAdminUserId),
    ('66666666-6666-6666-6666-666666666607', @TenantId, '55555555-5555-5555-5555-555555555507', N'Hiring manager review email', N'Hiring Manager', N'Final review ready: {{candidateName}}', N'{{candidateName}} is ready for final hiring-manager review for {{jobTitle}}.', N'["candidateName","jobTitle"]', N'Active', @TenantAdminUserId)
) AS source (NotificationTemplateId, TenantId, NotificationEventId, Name, Recipient, Subject, Body, AllowedVariablesJson, Status, UpdatedByUserId)
ON target.NotificationTemplateId = source.NotificationTemplateId
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
    (N'requirement-parser', N'Requirement Parser', N'Extracts structured hiring requirements from resource requests and job descriptions.', N'Job request title, description, skills, seniority, location, and hiring context.', N'Structured requirement profile.', N'AI is advisory and does not approve or reject requests.', CAST(1 AS BIT)),
    (N'cv-parser', N'CV Parser', N'Parses DOCX resumes into candidate profile and matching evidence.', N'DOCX text extracted server-side.', N'Structured candidate profile and skill evidence.', N'DOCX only for MVP; recruiters review extracted data.', CAST(1 AS BIT)),
    (N'bench-matching', N'Bench Matching', N'Recommends currently benched employees to PMO.', N'Job requirement profile and active benched employee profiles.', N'Ranked employee matches with fit evidence.', N'PMO decides whether to refer an employee.', CAST(1 AS BIT)),
    (N'talent-rediscovery', N'Talent Rediscovery', N'Prioritizes previous similar-job candidates before external sourcing.', N'Historical applications, interview outcomes, and requirement profile.', N'Ranked warm candidates.', N'Recruiters decide who to contact.', CAST(1 AS BIT)),
    (N'fit-explanation', N'Fit Explanation', N'Explains why an employee or candidate was recommended.', N'Recommendation evidence, skills, experience, and gaps.', N'Readable strengths, gaps, and confidence notes.', N'Explanation supports human review only.', CAST(1 AS BIT)),
    (N'hiring-manager-decision-brief', N'Hiring Manager Decision Brief', N'Summarizes interview feedback and candidate context for final human review.', N'Interview feedback, application history, and candidate profile.', N'Decision brief for Hiring Manager.', N'Hiring Manager owns the final decision.', CAST(1 AS BIT))
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
