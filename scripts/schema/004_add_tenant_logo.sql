IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'LogoFileName') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD LogoFileName NVARCHAR(260) NULL;
END;
GO

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'LogoContentType') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD LogoContentType NVARCHAR(100) NULL;
END;
GO

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'LogoContent') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD LogoContent VARBINARY(MAX) NULL;
END;
GO
