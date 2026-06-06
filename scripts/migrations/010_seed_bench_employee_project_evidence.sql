/*
    Adds demo project evidence for benched employees so Bench Matching can explain
    internal project/client relevance without relying on fabricated context.
*/

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @EngineeringDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';
DECLARE @DevOpsDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03';
DECLARE @HamzaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd04';
DECLARE @AminaEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd05';
DECLARE @UsmanEmployeeId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddd06';
DECLARE @ProjectReliaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03';
DECLARE @ProjectCloudOpsId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee04';
DECLARE @AssignmentHamzaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee23';
DECLARE @AssignmentAminaId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee24';
DECLARE @AssignmentUsmanId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeee25';

IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantId = @TenantId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE TenantId = @TenantId AND DepartmentId = @EngineeringDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE TenantId = @TenantId AND DepartmentId = @DevOpsDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Employees WHERE TenantId = @TenantId AND EmployeeId = @HamzaEmployeeId)
   AND EXISTS (SELECT 1 FROM dbo.Employees WHERE TenantId = @TenantId AND EmployeeId = @AminaEmployeeId)
   AND EXISTS (SELECT 1 FROM dbo.Employees WHERE TenantId = @TenantId AND EmployeeId = @UsmanEmployeeId)
BEGIN
    MERGE dbo.Projects AS target
    USING (VALUES
        (@ProjectReliaId, @TenantId, @EngineeringDepartmentId, N'REL', N'Relia Operations Portal', N'Relia', N'Closed', CONVERT(date, '2024-01-15'), CONVERT(date, '2024-12-20')),
        (@ProjectCloudOpsId, @TenantId, @DevOpsDepartmentId, N'CLO', N'CloudOps Automation Platform', N'Enterprise Client', N'Closed', CONVERT(date, '2023-04-01'), CONVERT(date, '2023-11-30'))
    ) AS source (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn)
    ON target.ProjectId = source.ProjectId
    WHEN MATCHED THEN
        UPDATE SET DepartmentId = source.DepartmentId, Code = source.Code, Name = source.Name, ClientName = source.ClientName, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (ProjectId, TenantId, DepartmentId, Code, Name, ClientName, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.ProjectId, source.TenantId, source.DepartmentId, source.Code, source.Name, source.ClientName, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);

    MERGE dbo.EmployeeProjectAssignments AS target
    USING (VALUES
        (@AssignmentHamzaId, @TenantId, @HamzaEmployeeId, @ProjectReliaId, 100, N'Completed', CONVERT(date, '2024-01-15'), CONVERT(date, '2024-12-20')),
        (@AssignmentAminaId, @TenantId, @AminaEmployeeId, @ProjectReliaId, 75, N'Completed', CONVERT(date, '2024-03-01'), CONVERT(date, '2024-10-31')),
        (@AssignmentUsmanId, @TenantId, @UsmanEmployeeId, @ProjectCloudOpsId, 100, N'Completed', CONVERT(date, '2023-04-01'), CONVERT(date, '2023-11-30'))
    ) AS source (ProjectAssignmentId, TenantId, EmployeeId, ProjectId, AllocationPercent, Status, StartsOn, EndsOn)
    ON target.ProjectAssignmentId = source.ProjectAssignmentId
    WHEN MATCHED THEN
        UPDATE SET AllocationPercent = source.AllocationPercent, Status = source.Status, StartsOn = source.StartsOn, EndsOn = source.EndsOn, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (ProjectAssignmentId, TenantId, EmployeeId, ProjectId, AllocationPercent, Status, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.ProjectAssignmentId, source.TenantId, source.EmployeeId, source.ProjectId, source.AllocationPercent, source.Status, source.StartsOn, source.EndsOn, @Now, @Now);
END;
GO
