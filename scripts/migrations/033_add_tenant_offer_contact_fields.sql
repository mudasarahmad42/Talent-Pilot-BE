IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'CompanyAddress') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD CompanyAddress NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'CompanyCity') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD CompanyCity NVARCHAR(120) NULL;
END;
GO

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'CompanyCountry') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD CompanyCountry NVARCHAR(120) NULL;
END;
GO

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'OfficialEmail') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD OfficialEmail NVARCHAR(320) NULL;
END;
GO

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'OfficialPhone') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD OfficialPhone NVARCHAR(50) NULL;
END;
GO

UPDATE dbo.TenantRecruitmentSettings
SET CompanyAddress = COALESCE(NULLIF(LTRIM(RTRIM(CompanyAddress)), N''), N'75-C/II, Gulberg III'),
    CompanyCity = COALESCE(NULLIF(LTRIM(RTRIM(CompanyCity)), N''), N'Lahore'),
    CompanyCountry = COALESCE(NULLIF(LTRIM(RTRIM(CompanyCountry)), N''), N'Pakistan'),
    OfficialEmail = COALESCE(NULLIF(LTRIM(RTRIM(OfficialEmail)), N''), N'hr@tkxel.com'),
    OfficialPhone = COALESCE(NULLIF(LTRIM(RTRIM(OfficialPhone)), N''), N'+92 42 111 859 351'),
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = '11111111-1111-1111-1111-111111111111';
GO
