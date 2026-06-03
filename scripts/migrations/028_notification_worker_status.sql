-- Migration 028: add notification worker heartbeat status for Admin Center diagnostics.

IF OBJECT_ID(N'dbo.NotificationWorkerStatus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationWorkerStatus
    (
        WorkerName NVARCHAR(120) NOT NULL CONSTRAINT PK_NotificationWorkerStatus PRIMARY KEY,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_NotificationWorkerStatus_Status DEFAULT N'Running',
        HostName NVARCHAR(200) NOT NULL,
        ProcessId INT NOT NULL,
        StartedAtUtc DATETIME2(3) NOT NULL,
        LastHeartbeatUtc DATETIME2(3) NOT NULL,
        LastProcessedAtUtc DATETIME2(3) NULL,
        LastProcessedCount INT NOT NULL CONSTRAINT DF_NotificationWorkerStatus_LastProcessedCount DEFAULT (0),
        LastError NVARCHAR(1000) NULL,
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationWorkerStatus_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_NotificationWorkerStatus_Status CHECK (Status IN (N'Running', N'Error'))
    );
END;
GO
