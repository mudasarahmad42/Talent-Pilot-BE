SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.Auth_GetCurrentUserContext
    @TenantId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.TenantId,
        u.DisplayName,
        u.Email,
        u.AccountStatus,
        t.DisplayName AS TenantDisplayName,
        COALESCE(tap.PermissionResolutionMode, N'MergeAllAssignedRoles') AS PermissionResolutionMode
    FROM dbo.AppUsers AS u
    INNER JOIN dbo.Tenants AS t ON t.TenantId = u.TenantId
    LEFT JOIN dbo.TenantAccessPolicies AS tap ON tap.TenantId = u.TenantId
    WHERE u.TenantId = @TenantId
      AND u.UserId = @UserId
      AND u.DeletedAtUtc IS NULL;

    SELECT
        r.RoleId,
        r.Code,
        r.Name,
        r.Priority,
        rp.PermissionId
    FROM dbo.UserRoles AS ur
    INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
    LEFT JOIN dbo.RolePermissions AS rp ON rp.RoleId = r.RoleId
    WHERE ur.TenantId = @TenantId
      AND ur.UserId = @UserId
      AND r.Status = N'Active'
    ORDER BY r.Priority, r.Name, rp.PermissionId;

    SELECT
        g.GroupId,
        g.Name,
        g.Purpose
    FROM dbo.GroupMembers AS gm
    INNER JOIN dbo.Groups AS g ON g.GroupId = gm.GroupId
    WHERE gm.TenantId = @TenantId
      AND gm.UserId = @UserId
      AND g.Status = N'Active'
    ORDER BY g.Name;
END;
GO

CREATE OR ALTER PROCEDURE dbo.AdminUsers_List
    @TenantId UNIQUEIDENTIFIER,
    @Search NVARCHAR(200) = NULL,
    @RoleId UNIQUEIDENTIFIER = NULL,
    @GroupId UNIQUEIDENTIFIER = NULL,
    @AccountStatus NVARCHAR(20) = NULL,
    @Page INT = 1,
    @PageSize INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SafePage INT = CASE WHEN @Page < 1 THEN 1 ELSE @Page END;
    DECLARE @SafePageSize INT = CASE WHEN @PageSize < 1 THEN 25 WHEN @PageSize > 100 THEN 100 ELSE @PageSize END;
    DECLARE @Offset INT = (@SafePage - 1) * @SafePageSize;
    DECLARE @SearchPattern NVARCHAR(204) = CASE WHEN NULLIF(LTRIM(RTRIM(@Search)), N'') IS NULL THEN NULL ELSE N'%' + LTRIM(RTRIM(@Search)) + N'%' END;

    CREATE TABLE #FilteredUsers
    (
        UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        Initials NVARCHAR(8) NOT NULL,
        AccountStatus NVARCHAR(20) NOT NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        DepartmentName NVARCHAR(200) NULL,
        ExperienceYears DECIMAL(4,1) NULL,
        JoiningDate DATE NULL,
        CompletedInterviewCount INT NOT NULL DEFAULT (0),
        LastActiveAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL,
        UpdatedAtUtc DATETIME2(3) NOT NULL
    );

    INSERT INTO #FilteredUsers (UserId, TenantId, DisplayName, Email, Initials, AccountStatus, DepartmentId, DepartmentName, ExperienceYears, JoiningDate, CompletedInterviewCount, LastActiveAtUtc, CreatedAtUtc, UpdatedAtUtc)
    SELECT
        u.UserId,
        u.TenantId,
        u.DisplayName,
        u.Email,
        u.Initials,
        u.AccountStatus,
        e.DepartmentId,
        d.Name AS DepartmentName,
        e.ExperienceYears,
        e.JoiningDate,
        COALESCE(interviewStats.CompletedInterviewCount, 0) AS CompletedInterviewCount,
        u.LastActiveAtUtc,
        u.CreatedAtUtc,
        u.UpdatedAtUtc
    FROM dbo.AppUsers AS u
    LEFT JOIN dbo.Employees AS e ON e.TenantId = u.TenantId AND e.AppUserId = u.UserId
    LEFT JOIN dbo.Departments AS d ON d.DepartmentId = e.DepartmentId
    OUTER APPLY
    (
        SELECT COUNT(1) AS CompletedInterviewCount
        FROM dbo.Interviews AS interview
        WHERE interview.TenantId = u.TenantId
          AND interview.InterviewerUserId = u.UserId
          AND interview.Status = N'Completed'
    ) AS interviewStats
    WHERE u.TenantId = @TenantId
      AND u.DeletedAtUtc IS NULL
      AND (@SearchPattern IS NULL OR u.DisplayName LIKE @SearchPattern OR u.Email LIKE @SearchPattern OR d.Name LIKE @SearchPattern)
      AND (@AccountStatus IS NULL OR u.AccountStatus = @AccountStatus)
      AND (@RoleId IS NULL OR EXISTS
      (
          SELECT 1
          FROM dbo.UserRoles AS ur
          WHERE ur.TenantId = u.TenantId
            AND ur.UserId = u.UserId
            AND ur.RoleId = @RoleId
      ))
      AND (@GroupId IS NULL OR EXISTS
      (
          SELECT 1
          FROM dbo.GroupMembers AS gm
          WHERE gm.TenantId = u.TenantId
            AND gm.UserId = u.UserId
            AND gm.GroupId = @GroupId
      ));

    SELECT
        (SELECT COUNT(1) FROM dbo.AppUsers WHERE TenantId = @TenantId AND DeletedAtUtc IS NULL) AS InternalUserCount,
        (SELECT COUNT(1) FROM dbo.Groups WHERE TenantId = @TenantId AND Purpose = N'WorkflowRouting' AND Status = N'Active') AS RoutingGroupCount,
        tap.BenchVisibilityRoleId AS BenchVisibilityRoleId,
        br.Name AS BenchVisibilityRoleName,
        N'RolesPermissions' AS BenchVisibilityConfiguredIn
    FROM dbo.TenantAccessPolicies AS tap
    INNER JOIN dbo.Roles AS br ON br.RoleId = tap.BenchVisibilityRoleId
    WHERE tap.TenantId = @TenantId;

    SELECT COUNT(1) AS TotalCount
    FROM #FilteredUsers;

    SELECT
        fu.UserId AS Id,
        fu.DisplayName,
        fu.Email,
        fu.Initials,
        roles.RoleIdsCsv,
        roles.RoleNamesCsv,
        highest.RoleId AS HighestPriorityRoleId,
        highest.Name AS HighestPriorityRoleName,
        highest.Priority AS HighestPriorityRolePriority,
        groups.GroupIdsCsv,
        groups.GroupNamesCsv,
        fu.DepartmentId,
        fu.DepartmentName,
        fu.ExperienceYears,
        fu.JoiningDate,
        fu.CompletedInterviewCount,
        fu.AccountStatus,
        fu.LastActiveAtUtc,
        fu.CreatedAtUtc,
        fu.UpdatedAtUtc
    FROM #FilteredUsers AS fu
    OUTER APPLY
    (
        SELECT TOP (1)
            r.RoleId,
            r.Name,
            r.Priority
        FROM dbo.UserRoles AS ur
        INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
        WHERE ur.TenantId = fu.TenantId
          AND ur.UserId = fu.UserId
          AND r.Status = N'Active'
        ORDER BY r.Priority, r.Name
    ) AS highest
    OUTER APPLY
    (
        SELECT
            STRING_AGG(CONVERT(NVARCHAR(36), r.RoleId), N',') AS RoleIdsCsv,
            STRING_AGG(r.Name, N',') AS RoleNamesCsv
        FROM dbo.UserRoles AS ur
        INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
        WHERE ur.TenantId = fu.TenantId
          AND ur.UserId = fu.UserId
          AND r.Status = N'Active'
    ) AS roles
    OUTER APPLY
    (
        SELECT
            STRING_AGG(CONVERT(NVARCHAR(36), g.GroupId), N',') AS GroupIdsCsv,
            STRING_AGG(g.Name, N',') AS GroupNamesCsv
        FROM dbo.GroupMembers AS gm
        INNER JOIN dbo.Groups AS g ON g.GroupId = gm.GroupId
        WHERE gm.TenantId = fu.TenantId
          AND gm.UserId = fu.UserId
          AND g.Status = N'Active'
    ) AS groups
    ORDER BY fu.DisplayName
    OFFSET @Offset ROWS FETCH NEXT @SafePageSize ROWS ONLY;
END;
GO

CREATE OR ALTER PROCEDURE dbo.AdminUsers_GetById
    @TenantId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId AS Id,
        u.DisplayName,
        u.Email,
        u.Initials,
        u.AccountStatus,
        u.LastActiveAtUtc,
        u.CreatedAtUtc,
        u.UpdatedAtUtc,
        roles.RoleIdsCsv,
        groups.GroupIdsCsv
    FROM dbo.AppUsers AS u
    OUTER APPLY
    (
        SELECT STRING_AGG(CONVERT(NVARCHAR(36), ur.RoleId), N',') AS RoleIdsCsv
        FROM dbo.UserRoles AS ur
        INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
        WHERE ur.TenantId = u.TenantId
          AND ur.UserId = u.UserId
          AND r.Status = N'Active'
    ) AS roles
    OUTER APPLY
    (
        SELECT STRING_AGG(CONVERT(NVARCHAR(36), gm.GroupId), N',') AS GroupIdsCsv
        FROM dbo.GroupMembers AS gm
        INNER JOIN dbo.Groups AS g ON g.GroupId = gm.GroupId
        WHERE gm.TenantId = u.TenantId
          AND gm.UserId = u.UserId
          AND g.Status = N'Active'
    ) AS groups
    WHERE u.TenantId = @TenantId
      AND u.UserId = @UserId
      AND u.DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.AdminUsers_SetAccountStatus
    @TenantId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @AccountStatus NVARCHAR(20),
    @ActorUserId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountStatus NOT IN (N'Active', N'Disabled', N'Invited')
    BEGIN
        THROW 51000, 'AccountStatus must be Active, Disabled, or Invited.', 1;
    END;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @ActorDisplayName NVARCHAR(200);
    DECLARE @UserDisplayName NVARCHAR(200);

    SELECT @ActorDisplayName = DisplayName
    FROM dbo.AppUsers
    WHERE TenantId = @TenantId AND UserId = @ActorUserId;

    SELECT @UserDisplayName = DisplayName
    FROM dbo.AppUsers
    WHERE TenantId = @TenantId AND UserId = @UserId AND DeletedAtUtc IS NULL;

    IF @UserDisplayName IS NULL
    BEGIN
        THROW 51001, 'User was not found for the tenant.', 1;
    END;

    BEGIN TRANSACTION;

    UPDATE dbo.AppUsers
    SET AccountStatus = @AccountStatus,
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId
      AND UserId = @UserId
      AND DeletedAtUtc IS NULL;

    INSERT INTO dbo.AuditLogs
    (
        AuditLogId,
        TenantId,
        OccurredAtUtc,
        ActorUserId,
        ActorDisplayName,
        EventType,
        EntityType,
        EntityId,
        RecordLabel,
        EventSummary,
        Area,
        MetadataJson
    )
    VALUES
    (
        NEWID(),
        @TenantId,
        @Now,
        @ActorUserId,
        COALESCE(@ActorDisplayName, N'System'),
        N'user.account_status.changed',
        N'User',
        @UserId,
        N'User access',
        CONCAT(@UserDisplayName, N' account status changed to ', @AccountStatus, N'.'),
        N'Admin Center',
        CONCAT(N'{"accountStatus":"', @AccountStatus, N'","reason":', CASE WHEN @Reason IS NULL THEN N'null' ELSE CONCAT(N'"', STRING_ESCAPE(@Reason, 'json'), N'"') END, N'}')
    );

    COMMIT TRANSACTION;

    EXEC dbo.AdminUsers_GetById @TenantId = @TenantId, @UserId = @UserId;
END;
GO
