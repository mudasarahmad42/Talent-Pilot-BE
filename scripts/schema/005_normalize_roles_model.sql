SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @SystemAdminRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222200';
DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

MERGE dbo.Roles AS target
USING (VALUES
    (@SystemAdminRoleId, CAST(NULL AS UNIQUEIDENTIFIER), N'SystemAdmin', N'System Admin', N'System', N'Platform', 1, CAST(1 AS BIT), N'Active')
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

UPDATE dbo.Roles
SET Type = N'Tenant',
    Scope = N'Tenant',
    IsProtected = 0,
    UpdatedAtUtc = @Now
WHERE TenantId IS NOT NULL
  AND (Type <> N'Tenant' OR Scope <> N'Tenant' OR IsProtected <> 0);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAtUtc)
SELECT @SystemAdminRoleId, p.PermissionId, @Now
FROM dbo.Permissions AS p
WHERE p.Status = N'Active'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RolePermissions AS rp
      WHERE rp.RoleId = @SystemAdminRoleId
        AND rp.PermissionId = p.PermissionId
  );
GO
