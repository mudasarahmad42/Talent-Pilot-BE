/*
    Adds durable daily usage tracking for external paid tools.
    Tavily web research uses this table to enforce the application-owned 60 requests/day cap.
*/

IF OBJECT_ID(N'dbo.ExternalToolDailyUsage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExternalToolDailyUsage
    (
        ExternalToolDailyUsageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExternalToolDailyUsage PRIMARY KEY,
        Provider NVARCHAR(80) NOT NULL,
        UsageDateUtc DATE NOT NULL,
        RequestCount INT NOT NULL CONSTRAINT DF_ExternalToolDailyUsage_RequestCount DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_ExternalToolDailyUsage_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_ExternalToolDailyUsage_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_ExternalToolDailyUsage_RequestCount CHECK (RequestCount >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_ExternalToolDailyUsage_Provider_Date'
      AND object_id = OBJECT_ID(N'dbo.ExternalToolDailyUsage')
)
BEGIN
    CREATE UNIQUE INDEX UX_ExternalToolDailyUsage_Provider_Date
        ON dbo.ExternalToolDailyUsage (Provider, UsageDateUtc);
END;
GO
