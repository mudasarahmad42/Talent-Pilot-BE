-- Migration 013: grant PMO users permission to create Job Requests.
-- Business rule: PMO-created requests stay assigned to the PMO creator for PMO Review.

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAtUtc)
SELECT r.RoleId, N'job.requests.create', @Now
FROM dbo.Roles AS r
INNER JOIN dbo.Permissions AS p
    ON p.PermissionId = N'job.requests.create'
WHERE r.Code = N'PMO'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.RolePermissions AS rp
      WHERE rp.RoleId = r.RoleId
        AND rp.PermissionId = N'job.requests.create'
  );
