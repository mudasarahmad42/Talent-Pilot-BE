IF OBJECT_ID(N'dbo.GoogleCalendarConnections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GoogleCalendarConnections
    (
        GoogleCalendarConnectionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GoogleCalendarConnections PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        OrganizerUserId UNIQUEIDENTIFIER NOT NULL,
        OrganizerEmail NVARCHAR(320) NOT NULL,
        Provider NVARCHAR(40) NOT NULL,
        RefreshTokenCiphertext NVARCHAR(MAX) NULL,
        AccessTokenCiphertext NVARCHAR(MAX) NULL,
        AccessTokenExpiresAtUtc DATETIME2(3) NULL,
        Scope NVARCHAR(600) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_GoogleCalendarConnections_Status DEFAULT N'Connected',
        ConnectedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GoogleCalendarConnections_ConnectedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GoogleCalendarConnections_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_GoogleCalendarConnections_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_GoogleCalendarConnections_AppUsers FOREIGN KEY (OrganizerUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_GoogleCalendarConnections_Status CHECK (Status IN (N'Connected', N'Disconnected'))
    );

    CREATE UNIQUE INDEX UX_GoogleCalendarConnections_Tenant_Provider
        ON dbo.GoogleCalendarConnections (TenantId, Provider);
END;
GO

IF OBJECT_ID(N'dbo.GoogleCalendarOAuthStates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GoogleCalendarOAuthStates
    (
        StateHash NVARCHAR(128) NOT NULL CONSTRAINT PK_GoogleCalendarOAuthStates PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        UserEmail NVARCHAR(320) NOT NULL,
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GoogleCalendarOAuthStates_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ConsumedAtUtc DATETIME2(3) NULL,
        CONSTRAINT FK_GoogleCalendarOAuthStates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_GoogleCalendarOAuthStates_AppUsers FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId)
    );

    CREATE INDEX IX_GoogleCalendarOAuthStates_Expiry
        ON dbo.GoogleCalendarOAuthStates (ExpiresAtUtc, ConsumedAtUtc);
END;
GO
