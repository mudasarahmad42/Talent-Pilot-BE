SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.TenantAccessPolicies', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TenantAccessPolicies', N'AdminCenterAccessMode') IS NULL
BEGIN
    ALTER TABLE dbo.TenantAccessPolicies
    ADD AdminCenterAccessMode NVARCHAR(20) NOT NULL
        CONSTRAINT DF_TenantAccessPolicies_AdminCenterAccessMode DEFAULT N'FullAccess';
END;
GO

IF OBJECT_ID(N'dbo.TenantAccessPolicies', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TenantAccessPolicies', N'AdminCenterAccessMode') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.check_constraints
       WHERE name = N'CK_TenantAccessPolicies_AdminCenterAccessMode'
         AND parent_object_id = OBJECT_ID(N'dbo.TenantAccessPolicies')
   )
BEGIN
    ALTER TABLE dbo.TenantAccessPolicies
    ADD CONSTRAINT CK_TenantAccessPolicies_AdminCenterAccessMode
        CHECK (AdminCenterAccessMode IN (N'FullAccess', N'ReadOnly'));
END;
GO
