-- Migration 029: add tenant-selectable notification email provider.

IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'NotificationEmailProvider') IS NULL
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings
        ADD NotificationEmailProvider NVARCHAR(40) NOT NULL
            CONSTRAINT DF_TenantRecruitmentSettings_NotificationEmailProvider DEFAULT N'Resend';
END;
GO

UPDATE dbo.TenantRecruitmentSettings
SET NotificationEmailProvider = N'Resend'
WHERE NotificationEmailProvider IS NULL
   OR NotificationEmailProvider NOT IN (N'Resend', N'MicrosoftGraph');
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_TenantRecruitmentSettings_NotificationEmailProvider'
      AND parent_object_id = OBJECT_ID(N'dbo.TenantRecruitmentSettings')
)
BEGIN
    ALTER TABLE dbo.TenantRecruitmentSettings WITH CHECK
        ADD CONSTRAINT CK_TenantRecruitmentSettings_NotificationEmailProvider
        CHECK (NotificationEmailProvider IN (N'Resend', N'MicrosoftGraph'));
END;
GO
