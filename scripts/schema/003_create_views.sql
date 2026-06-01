SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER VIEW dbo.vw_PublicJobs
AS
SELECT
    jr.TenantId,
    jr.JobRequestId,
    jr.RequestCode,
    jr.Title,
    jr.Description,
    d.Name AS DepartmentName,
    l.Name AS LocationName,
    jr.EmploymentType,
    jr.ExperienceMinYears,
    jr.ExperienceMaxYears,
    jr.RequiredPositions,
    jr.FulfilledPositions,
    jr.PublishedAtUtc,
    jr.UpdatedAtUtc
FROM dbo.JobRequests AS jr
LEFT JOIN dbo.Departments AS d ON d.DepartmentId = jr.DepartmentId
LEFT JOIN dbo.Locations AS l ON l.LocationId = jr.LocationId
INNER JOIN dbo.TenantRecruitmentSettings AS trs ON trs.TenantId = jr.TenantId
WHERE
    trs.PublicJobsEnabled = 1
    AND jr.PublishStatus = N'Published'
    AND jr.Status IN (N'Sourcing', N'Interviewing', N'HiringManagerReview', N'Offer');
GO

CREATE OR ALTER VIEW dbo.vw_ActiveWorkflowAssignments
AS
SELECT
    wa.TenantId,
    wa.WorkflowAssignmentId,
    wa.EntityType,
    wa.EntityId,
    wa.AssignmentStatus,
    wa.AssignedToUserId,
    assignedUser.DisplayName AS AssignedToUserName,
    wa.AssignedToGroupId,
    assignedGroup.Name AS AssignedToGroupName,
    wa.AssignedToRoleId,
    assignedRole.Name AS AssignedToRoleName,
    wa.ClaimedByUserId,
    claimedUser.DisplayName AS ClaimedByUserName,
    wa.AssignedAtUtc,
    wa.ClaimedAtUtc,
    ws.StageKey,
    ws.Name AS StageName
FROM dbo.WorkflowAssignments AS wa
INNER JOIN dbo.WorkflowStages AS ws ON ws.WorkflowStageId = wa.WorkflowStageId
LEFT JOIN dbo.AppUsers AS assignedUser ON assignedUser.UserId = wa.AssignedToUserId
LEFT JOIN dbo.Groups AS assignedGroup ON assignedGroup.GroupId = wa.AssignedToGroupId
LEFT JOIN dbo.Roles AS assignedRole ON assignedRole.RoleId = wa.AssignedToRoleId
LEFT JOIN dbo.AppUsers AS claimedUser ON claimedUser.UserId = wa.ClaimedByUserId
WHERE wa.AssignmentStatus IN (N'Pending', N'Claimed');
GO

CREATE OR ALTER VIEW dbo.vw_JobRequestDashboard
AS
SELECT
    jr.TenantId,
    jr.JobRequestId,
    jr.RequestCode,
    jr.Title,
    jr.ClientName,
    jr.Status,
    jr.PublishStatus,
    jr.CurrentStageKey,
    jr.RequiredPositions,
    jr.FulfilledPositions,
    jr.CreatedAtUtc,
    jr.UpdatedAtUtc,
    creator.DisplayName AS CreatedByName,
    hm.DisplayName AS HiringManagerName,
    d.Name AS DepartmentName,
    l.Name AS LocationName,
    activeAssignment.WorkflowAssignmentId,
    activeAssignment.AssignmentStatus,
    activeAssignment.AssignedToUserName,
    activeAssignment.AssignedToGroupName,
    activeAssignment.AssignedToRoleName
FROM dbo.JobRequests AS jr
INNER JOIN dbo.AppUsers AS creator ON creator.UserId = jr.CreatedByUserId
LEFT JOIN dbo.AppUsers AS hm ON hm.UserId = jr.HiringManagerUserId
LEFT JOIN dbo.Departments AS d ON d.DepartmentId = jr.DepartmentId
LEFT JOIN dbo.Locations AS l ON l.LocationId = jr.LocationId
LEFT JOIN dbo.vw_ActiveWorkflowAssignments AS activeAssignment
    ON activeAssignment.TenantId = jr.TenantId
    AND activeAssignment.EntityType = N'JobRequest'
    AND activeAssignment.EntityId = jr.JobRequestId;
GO

CREATE OR ALTER VIEW dbo.vw_EmployeeBenchAvailability
AS
SELECT
    e.TenantId,
    e.EmployeeId,
    e.AppUserId,
    e.EmployeeCode,
    e.DisplayName,
    e.Email,
    e.Designation,
    e.ExperienceYears,
    e.JoiningDate,
    d.Name AS DepartmentName,
    l.Name AS LocationName,
    e.AvailabilityStatus,
    e.BenchStatus,
    CASE
        WHEN activeAssignment.ProjectAssignmentId IS NULL THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END AS IsCurrentlyBenched,
    activeAssignment.ProjectId,
    activeAssignment.AllocationPercent,
    activeAssignment.StartsOn,
    activeAssignment.EndsOn
FROM dbo.Employees AS e
LEFT JOIN dbo.Departments AS d ON d.DepartmentId = e.DepartmentId
LEFT JOIN dbo.Locations AS l ON l.LocationId = e.LocationId
OUTER APPLY
(
    SELECT TOP (1)
        epa.ProjectAssignmentId,
        epa.ProjectId,
        epa.AllocationPercent,
        epa.StartsOn,
        epa.EndsOn
    FROM dbo.EmployeeProjectAssignments AS epa
    WHERE
        epa.TenantId = e.TenantId
        AND epa.EmployeeId = e.EmployeeId
        AND epa.Status = N'Active'
        AND epa.StartsOn <= CONVERT(date, SYSUTCDATETIME())
        AND (epa.EndsOn IS NULL OR epa.EndsOn >= CONVERT(date, SYSUTCDATETIME()))
    ORDER BY epa.StartsOn DESC
) AS activeAssignment
WHERE e.Status = N'Active';
GO
