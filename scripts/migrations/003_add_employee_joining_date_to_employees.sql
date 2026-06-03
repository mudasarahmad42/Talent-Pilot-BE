SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.Employees', N'JoiningDate') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD JoiningDate DATE NULL;
END;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @PresalesUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333302';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @PresalesDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05';
DECLARE @RecruitmentDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06';
DECLARE @KarachiLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01';
DECLARE @LahoreLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02';
DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

UPDATE dbo.Employees
SET JoiningDate = CASE Email
    WHEN N'pmo@tkxel.com' THEN CONVERT(date, '2018-06-04')
    WHEN N'interviewer@tkxel.com' THEN CONVERT(date, '2020-04-20')
    WHEN N'hiring.manager@tkxel.com' THEN CONVERT(date, '2016-09-05')
    WHEN N'hamza.ali@tkxel.com' THEN CONVERT(date, '2021-08-02')
    WHEN N'amina.shah@tkxel.com' THEN CONVERT(date, '2022-05-09')
    WHEN N'usman.tariq@tkxel.com' THEN CONVERT(date, '2021-11-15')
    ELSE JoiningDate
END
WHERE TenantId = @TenantId
  AND Email IN (
      N'pmo@tkxel.com',
      N'interviewer@tkxel.com',
      N'hiring.manager@tkxel.com',
      N'hamza.ali@tkxel.com',
      N'amina.shah@tkxel.com',
      N'usman.tariq@tkxel.com'
  )
  AND JoiningDate IS NULL;

IF EXISTS (SELECT 1 FROM dbo.AppUsers WHERE TenantId = @TenantId AND UserId = @PresalesUserId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE TenantId = @TenantId AND DepartmentId = @PresalesDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Locations WHERE TenantId = @TenantId AND LocationId = @KarachiLocationId)
   AND NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeId = 'dddddddd-dddd-dddd-dddd-dddddddddd07')
   AND NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE TenantId = @TenantId AND Email = N'presales@tkxel.com')
BEGIN
    INSERT INTO dbo.Employees
    (
        EmployeeId,
        TenantId,
        AppUserId,
        EmployeeCode,
        ExternalEmployeeId,
        DisplayName,
        Email,
        DepartmentId,
        LocationId,
        Designation,
        ExperienceYears,
        JoiningDate,
        AvailabilityStatus,
        BenchStatus,
        Status,
        CreatedAtUtc,
        UpdatedAtUtc
    )
    VALUES
    (
        'dddddddd-dddd-dddd-dddd-dddddddddd07',
        @TenantId,
        @PresalesUserId,
        N'TKX-1007',
        N'EXT-1007',
        N'Ahmed Raza',
        N'presales@tkxel.com',
        @PresalesDepartmentId,
        @KarachiLocationId,
        N'Presales Consultant',
        CAST(7.0 AS DECIMAL(4,1)),
        CONVERT(date, '2019-03-11'),
        N'Allocated',
        N'Allocated',
        N'Active',
        @Now,
        @Now
    );
END;

IF EXISTS (SELECT 1 FROM dbo.AppUsers WHERE TenantId = @TenantId AND UserId = @RecruiterUserId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE TenantId = @TenantId AND DepartmentId = @RecruitmentDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Locations WHERE TenantId = @TenantId AND LocationId = @LahoreLocationId)
   AND NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeId = 'dddddddd-dddd-dddd-dddd-dddddddddd08')
   AND NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE TenantId = @TenantId AND Email = N'recruiter@tkxel.com')
BEGIN
    INSERT INTO dbo.Employees
    (
        EmployeeId,
        TenantId,
        AppUserId,
        EmployeeCode,
        ExternalEmployeeId,
        DisplayName,
        Email,
        DepartmentId,
        LocationId,
        Designation,
        ExperienceYears,
        JoiningDate,
        AvailabilityStatus,
        BenchStatus,
        Status,
        CreatedAtUtc,
        UpdatedAtUtc
    )
    VALUES
    (
        'dddddddd-dddd-dddd-dddd-dddddddddd08',
        @TenantId,
        @RecruiterUserId,
        N'TKX-1008',
        N'EXT-1008',
        N'Sara Malik',
        N'recruiter@tkxel.com',
        @RecruitmentDepartmentId,
        @LahoreLocationId,
        N'Talent Acquisition Specialist',
        CAST(5.0 AS DECIMAL(4,1)),
        CONVERT(date, '2021-01-18'),
        N'Allocated',
        N'Allocated',
        N'Active',
        @Now,
        @Now
    );
END;
GO
