IF OBJECT_ID(N'dbo.NotificationRecipients', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_NotificationRecipients_Event_User'
          AND parent_object_id = OBJECT_ID(N'dbo.NotificationRecipients')
    )
    BEGIN
        ALTER TABLE dbo.NotificationRecipients DROP CONSTRAINT UQ_NotificationRecipients_Event_User;
    END;

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'Title') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD Title NVARCHAR(200) NULL;

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'Message') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD Message NVARCHAR(1000) NULL;

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'Category') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD Category NVARCHAR(80) NOT NULL CONSTRAINT DF_NotificationRecipients_Category DEFAULT N'Workflow';

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'Severity') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD Severity NVARCHAR(20) NOT NULL CONSTRAINT DF_NotificationRecipients_Severity DEFAULT N'Info';

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'EntityType') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD EntityType NVARCHAR(80) NULL;

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'EntityId') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD EntityId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH(N'dbo.NotificationRecipients', N'MetadataJson') IS NULL
        ALTER TABLE dbo.NotificationRecipients ADD MetadataJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_NotificationRecipients_MetadataJson DEFAULT N'{}';
END;
GO

IF OBJECT_ID(N'dbo.NotificationRecipients', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_NotificationRecipients_MetadataJson'
          AND parent_object_id = OBJECT_ID(N'dbo.NotificationRecipients')
    )
    BEGIN
        ALTER TABLE dbo.NotificationRecipients
        ADD CONSTRAINT CK_NotificationRecipients_MetadataJson CHECK (ISJSON(MetadataJson) = 1);
    END;
END;
GO

INSERT INTO dbo.NotificationEvents
(
    NotificationEventId,
    TenantId,
    EventCode,
    Name,
    DefaultRecipientType,
    Status,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    NEWID(),
    t.TenantId,
    N'REALTIME_NOTIFICATION',
    N'Realtime notification',
    N'Realtime',
    N'Active',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM dbo.Tenants AS t
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.NotificationEvents AS ne
    WHERE ne.TenantId = t.TenantId
      AND ne.EventCode = N'REALTIME_NOTIFICATION'
);
GO
