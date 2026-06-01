-- 021_add_hod_role_and_demo_user.sql
-- Adds the tenant-scoped HOD / Department Head role and demo Engineering HOD user.
-- HOD participates as an interviewer/final-round recommendation only; it is not a system role or approval workflow.

SET NOCOUNT ON;

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @TenantAdminUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333301';
DECLARE @HodRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222209';
DECLARE @HodUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333311';
DECLARE @HodCredentialId UNIQUEIDENTIFIER = '77777777-7777-7777-7777-777777777311';
DECLARE @HodEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd09';
DECLARE @EngineeringDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';
DECLARE @LahoreLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02';
DECLARE @InterviewPanelEngineeringGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444403';
DECLARE @ReactSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc05';
DECLARE @DotNetSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc02';
DECLARE @AzureSkillId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccc04';

IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
BEGIN
    MERGE dbo.Roles AS target
    USING (VALUES (@HodRoleId, @TenantId, N'HOD', N'HOD / Department Head', N'Tenant', N'Tenant', 45, CAST(0 AS BIT), N'Active'))
    AS source (RoleId, TenantId, Code, Name, Type, Scope, Priority, IsProtected, Status)
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
END;

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NOT NULL
BEGIN
    MERGE dbo.AppUsers AS target
    USING (VALUES (@HodUserId, @TenantId, N'Zara Siddiqui', N'hod.engineering@tkxel.com', N'hod.engineering@tkxel.com', N'ZS', N'Active'))
    AS source (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus)
    ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            TenantId = source.TenantId,
            DisplayName = source.DisplayName,
            Email = source.Email,
            EmailNormalized = source.EmailNormalized,
            Initials = source.Initials,
            AccountStatus = source.AccountStatus,
            UpdatedAtUtc = @Now,
            DeletedAtUtc = NULL
    WHEN NOT MATCHED THEN
        INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.UserId, source.TenantId, source.DisplayName, source.Email, source.EmailNormalized, source.Initials, source.AccountStatus, @Now, @Now);
END;

IF OBJECT_ID(N'dbo.UserCredentials', N'U') IS NOT NULL
BEGIN
    MERGE dbo.UserCredentials AS target
    USING (VALUES (@HodCredentialId, @TenantId, @HodUserId, CAST(NULL AS NVARCHAR(500))))
    AS source (UserCredentialId, TenantId, UserId, PasswordHash)
    ON target.UserCredentialId = source.UserCredentialId
    WHEN MATCHED THEN
        UPDATE SET TenantId = source.TenantId, UserId = source.UserId, PasswordHash = source.PasswordHash, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserCredentialId, TenantId, UserId, PasswordHash, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.UserCredentialId, source.TenantId, source.UserId, source.PasswordHash, @Now, @Now);
END;

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL
BEGIN
    MERGE dbo.UserRoles AS target
    USING (VALUES (@TenantId, @HodUserId, @HodRoleId, @TenantAdminUserId))
    AS source (TenantId, UserId, RoleId, AssignedByUserId)
    ON target.TenantId = source.TenantId AND target.UserId = source.UserId AND target.RoleId = source.RoleId
    WHEN NOT MATCHED THEN
        INSERT (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
        VALUES (source.TenantId, source.UserId, source.RoleId, source.AssignedByUserId, @Now);
END;

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Permissions', N'U') IS NOT NULL
BEGIN
    MERGE dbo.RolePermissions AS target
    USING (
        SELECT @HodRoleId AS RoleId, permissionSource.PermissionId
        FROM (VALUES (N'workflow.assignments.claim'), (N'interviews.manage')) AS permissionSource (PermissionId)
        INNER JOIN dbo.Permissions AS permission ON permission.PermissionId = permissionSource.PermissionId
    ) AS source (RoleId, PermissionId)
    ON target.RoleId = source.RoleId AND target.PermissionId = source.PermissionId
    WHEN NOT MATCHED THEN
        INSERT (RoleId, PermissionId, CreatedAtUtc)
        VALUES (source.RoleId, source.PermissionId, @Now);
END;

IF OBJECT_ID(N'dbo.GroupMembers', N'U') IS NOT NULL
BEGIN
    MERGE dbo.GroupMembers AS target
    USING (VALUES (@TenantId, @InterviewPanelEngineeringGroupId, @HodUserId, CAST(0 AS BIT)))
    AS source (TenantId, GroupId, UserId, IsDefaultAssignee)
    ON target.TenantId = source.TenantId AND target.GroupId = source.GroupId AND target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET IsDefaultAssignee = source.IsDefaultAssignee
    WHEN NOT MATCHED THEN
        INSERT (TenantId, GroupId, UserId, IsDefaultAssignee, CreatedAtUtc)
        VALUES (source.TenantId, source.GroupId, source.UserId, source.IsDefaultAssignee, @Now);
END;

IF OBJECT_ID(N'dbo.Departments', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.Departments
    SET LeadUserId = @HodUserId, UpdatedAtUtc = @Now
    WHERE DepartmentId = @EngineeringDepartmentId AND TenantId = @TenantId;
END;

IF OBJECT_ID(N'dbo.Employees', N'U') IS NOT NULL
BEGIN
    MERGE dbo.Employees AS target
    USING (VALUES (
        @HodEmployeeId,
        @TenantId,
        @HodUserId,
        N'TKX-1009',
        N'EXT-1009',
        N'Zara Siddiqui',
        N'hod.engineering@tkxel.com',
        @EngineeringDepartmentId,
        @LahoreLocationId,
        N'Head of Engineering',
        CAST(13.0 AS DECIMAL(4,1)),
        CONVERT(date, '2014-02-10'),
        N'Allocated',
        N'Allocated',
        N'Active'
    ))
    AS source (EmployeeId, TenantId, AppUserId, EmployeeCode, ExternalEmployeeId, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, JoiningDate, AvailabilityStatus, BenchStatus, Status)
    ON target.EmployeeId = source.EmployeeId
    WHEN MATCHED THEN
        UPDATE SET
            AppUserId = source.AppUserId,
            DisplayName = source.DisplayName,
            Email = source.Email,
            DepartmentId = source.DepartmentId,
            LocationId = source.LocationId,
            Designation = source.Designation,
            ExperienceYears = source.ExperienceYears,
            JoiningDate = source.JoiningDate,
            AvailabilityStatus = source.AvailabilityStatus,
            BenchStatus = source.BenchStatus,
            Status = source.Status,
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (EmployeeId, TenantId, AppUserId, EmployeeCode, ExternalEmployeeId, DisplayName, Email, DepartmentId, LocationId, Designation, ExperienceYears, JoiningDate, AvailabilityStatus, BenchStatus, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.EmployeeId, source.TenantId, source.AppUserId, source.EmployeeCode, source.ExternalEmployeeId, source.DisplayName, source.Email, source.DepartmentId, source.LocationId, source.Designation, source.ExperienceYears, source.JoiningDate, source.AvailabilityStatus, source.BenchStatus, source.Status, @Now, @Now);
END;

IF OBJECT_ID(N'dbo.EmployeeSkills', N'U') IS NOT NULL
BEGIN
    MERGE dbo.EmployeeSkills AS target
    USING (VALUES
        (@TenantId, @HodEmployeeId, @ReactSkillId, N'Advanced', CAST(8.0 AS DECIMAL(4,1)), CAST(1 AS BIT)),
        (@TenantId, @HodEmployeeId, @DotNetSkillId, N'Advanced', CAST(8.0 AS DECIMAL(4,1)), CAST(0 AS BIT)),
        (@TenantId, @HodEmployeeId, @AzureSkillId, N'Advanced', CAST(6.0 AS DECIMAL(4,1)), CAST(0 AS BIT))
    ) AS source (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary)
    ON target.TenantId = source.TenantId AND target.EmployeeId = source.EmployeeId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
    WHEN NOT MATCHED THEN
        INSERT (TenantId, EmployeeId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
        VALUES (source.TenantId, source.EmployeeId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary, @Now);
END;

IF OBJECT_ID(N'dbo.InterviewTemplateRounds', N'U') IS NOT NULL
BEGIN
    UPDATE templateRound
    SET OwnerRoleId = @HodRoleId,
        OwnerUserId = @HodUserId
    FROM dbo.InterviewTemplateRounds AS templateRound
    INNER JOIN dbo.InterviewTemplates AS template
        ON template.InterviewTemplateId = templateRound.InterviewTemplateId
       AND template.TenantId = templateRound.TenantId
    WHERE templateRound.TenantId = @TenantId
      AND templateRound.Name LIKE N'%Department Head%'
      AND (template.DepartmentId = @EngineeringDepartmentId OR template.DepartmentId IS NULL);
END;

IF OBJECT_ID(N'dbo.JobRequestInterviewRounds', N'U') IS NOT NULL
BEGIN
    UPDATE requestRound
    SET OwnerRoleId = @HodRoleId,
        OwnerUserId = @HodUserId
    FROM dbo.JobRequestInterviewRounds AS requestRound
    INNER JOIN dbo.JobRequests AS request
        ON request.JobRequestId = requestRound.JobRequestId
       AND request.TenantId = requestRound.TenantId
    WHERE requestRound.TenantId = @TenantId
      AND requestRound.Name LIKE N'%Department Head%'
      AND request.DepartmentId = @EngineeringDepartmentId;
END;
GO
