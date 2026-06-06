/*
    Microsoft Graph is the production default notification email sender.
    Keep existing databases aligned with fresh schema/seed defaults.
*/
IF COL_LENGTH(N'dbo.TenantRecruitmentSettings', N'NotificationEmailProvider') IS NOT NULL
BEGIN
    UPDATE dbo.TenantRecruitmentSettings
    SET NotificationEmailProvider = N'MicrosoftGraph',
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE NotificationEmailProvider IS NULL
       OR NotificationEmailProvider = N'Resend'
       OR NotificationEmailProvider NOT IN (N'Resend', N'MicrosoftGraph');
END;
GO
