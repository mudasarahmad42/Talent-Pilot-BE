SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenants
    (
        TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
        DisplayName NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(80) NOT NULL,
        Domain NVARCHAR(255) NOT NULL,
        AdminContactEmail NVARCHAR(320) NOT NULL,
        DefaultTimezoneId NVARCHAR(100) NOT NULL,
        DefaultCurrencyCode CHAR(3) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT N'Active',
        SetupComplete BIT NOT NULL CONSTRAINT DF_Tenants_SetupComplete DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Tenants_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Tenants_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_Tenants_Slug UNIQUE (Slug),
        CONSTRAINT CK_Tenants_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT CK_Tenants_DefaultCurrencyCode CHECK (DefaultCurrencyCode IN ('PKR', 'USD', 'EUR'))
    );
END;
GO

IF OBJECT_ID(N'dbo.TenantRecruitmentSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantRecruitmentSettings
    (
        TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantRecruitmentSettings PRIMARY KEY,
        CareerDisplayName NVARCHAR(200) NOT NULL,
        CompanyAddress NVARCHAR(500) NULL,
        CompanyCity NVARCHAR(120) NULL,
        CompanyCountry NVARCHAR(120) NULL,
        OfficialEmail NVARCHAR(320) NULL,
        OfficialPhone NVARCHAR(50) NULL,
        PrimaryColorHex NVARCHAR(20) NOT NULL,
        CandidateLoginRequired BIT NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_CandidateLoginRequired DEFAULT (1),
        CandidateCvFormat NVARCHAR(20) NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_CandidateCvFormat DEFAULT N'DOCX',
        PublicJobsEnabled BIT NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_PublicJobsEnabled DEFAULT (1),
        InviteExpiryDays INT NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_InviteExpiryDays DEFAULT (7),
        ReapplyCooldownDays INT NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_ReapplyCooldownDays DEFAULT (90),
        NotificationEmailProvider NVARCHAR(40) NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_NotificationEmailProvider DEFAULT N'MicrosoftGraph',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TenantRecruitmentSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantRecruitmentSettings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_TenantRecruitmentSettings_CandidateCvFormat CHECK (CandidateCvFormat = N'DOCX'),
        CONSTRAINT CK_TenantRecruitmentSettings_InviteExpiryDays CHECK (InviteExpiryDays BETWEEN 1 AND 30),
        CONSTRAINT CK_TenantRecruitmentSettings_ReapplyCooldownDays CHECK (ReapplyCooldownDays BETWEEN 1 AND 365),
        CONSTRAINT CK_TenantRecruitmentSettings_NotificationEmailProvider CHECK (NotificationEmailProvider IN (N'Resend', N'MicrosoftGraph'))
    );
END;
GO

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        EmailNormalized NVARCHAR(320) NOT NULL,
        Initials NVARCHAR(8) NOT NULL,
        AccountStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_AppUsers_AccountStatus DEFAULT N'Invited',
        LastActiveAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppUsers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        DeletedAtUtc DATETIME2(3) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_AppUsers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_AppUsers_AccountStatus CHECK (AccountStatus IN (N'Active', N'Disabled', N'Invited'))
    );
END;
GO

IF OBJECT_ID(N'dbo.UserCredentials', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserCredentials
    (
        UserCredentialId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserCredentials PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        PasswordHash NVARCHAR(500) NULL,
        PasswordUpdatedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_UserCredentials_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_UserCredentials_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_UserCredentials_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_UserCredentials_AppUsers FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT UQ_UserCredentials_UserId UNIQUE (UserId)
    );
END;
GO

IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        RefreshTokenId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(256) NOT NULL,
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        RevokedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_RefreshTokens_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_RefreshTokens_AppUsers FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash)
    );
END;
GO

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        RoleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        Type NVARCHAR(20) NOT NULL,
        Scope NVARCHAR(20) NOT NULL,
        Priority INT NOT NULL,
        IsProtected BIT NOT NULL CONSTRAINT DF_Roles_IsProtected DEFAULT (0),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Roles_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Roles_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_Roles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_Roles_Type CHECK (Type IN (N'System', N'Tenant', N'Custom')),
        CONSTRAINT CK_Roles_Scope CHECK (Scope IN (N'Platform', N'Tenant', N'Portal')),
        CONSTRAINT CK_Roles_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT CK_Roles_Priority CHECK (Priority >= 1),
        CONSTRAINT UQ_Roles_Tenant_Code UNIQUE (TenantId, Code)
    );
END;
GO

IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions
    (
        PermissionId NVARCHAR(120) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
        DisplayName NVARCHAR(160) NOT NULL,
        GroupName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Permissions_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Permissions_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Permissions_Status CHECK (Status IN (N'Active', N'Inactive'))
    );
END;
GO

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions
    (
        RoleId UNIQUEIDENTIFIER NOT NULL,
        PermissionId NVARCHAR(120) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_RolePermissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (PermissionId)
    );
END;
GO

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        AssignedByUserId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_UserRoles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_UserRoles PRIMARY KEY (TenantId, UserId, RoleId),
        CONSTRAINT FK_UserRoles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_UserRoles_AppUsers FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT FK_UserRoles_AssignedByUser FOREIGN KEY (AssignedByUserId) REFERENCES dbo.AppUsers (UserId)
    );
END;
GO

IF OBJECT_ID(N'dbo.Groups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Groups
    (
        GroupId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Groups PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        Purpose NVARCHAR(80) NOT NULL,
        DefaultOwnerUserId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Groups_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Groups_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Groups_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_Groups_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_Groups_DefaultOwnerUser FOREIGN KEY (DefaultOwnerUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_Groups_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_Groups_Tenant_Purpose_Name UNIQUE (TenantId, Purpose, Name)
    );
END;
GO

IF OBJECT_ID(N'dbo.GroupMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GroupMembers
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        GroupId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        IsDefaultAssignee BIT NOT NULL CONSTRAINT DF_GroupMembers_IsDefaultAssignee DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GroupMembers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_GroupMembers PRIMARY KEY (TenantId, GroupId, UserId),
        CONSTRAINT FK_GroupMembers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_GroupMembers_Groups FOREIGN KEY (GroupId) REFERENCES dbo.Groups (GroupId),
        CONSTRAINT FK_GroupMembers_AppUsers FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId)
    );
END;
GO

IF OBJECT_ID(N'dbo.TenantAccessPolicies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantAccessPolicies
    (
        TenantAccessPolicyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantAccessPolicies PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        PermissionResolutionMode NVARCHAR(40) NOT NULL CONSTRAINT DF_TenantAccessPolicies_PermissionResolutionMode DEFAULT N'MergeAllAssignedRoles',
        BenchVisibilityRoleId UNIQUEIDENTIFIER NOT NULL,
        GroupFallbackMode NVARCHAR(80) NOT NULL CONSTRAINT DF_TenantAccessPolicies_GroupFallbackMode DEFAULT N'TenantAdmins',
        AdminCenterAccessMode NVARCHAR(20) NOT NULL CONSTRAINT DF_TenantAccessPolicies_AdminCenterAccessMode DEFAULT N'FullAccess',
        UpdatedByUserId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TenantAccessPolicies_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TenantAccessPolicies_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantAccessPolicies_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_TenantAccessPolicies_BenchVisibilityRole FOREIGN KEY (BenchVisibilityRoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT FK_TenantAccessPolicies_UpdatedByUser FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT UQ_TenantAccessPolicies_TenantId UNIQUE (TenantId),
        CONSTRAINT CK_TenantAccessPolicies_PermissionResolutionMode CHECK (PermissionResolutionMode IN (N'MergeAllAssignedRoles', N'HighestPriorityRoleOnly')),
        CONSTRAINT CK_TenantAccessPolicies_AdminCenterAccessMode CHECK (AdminCenterAccessMode IN (N'FullAccess', N'ReadOnly'))
    );
END;
GO

IF OBJECT_ID(N'dbo.NotificationEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationEvents
    (
        NotificationEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationEvents PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EventCode NVARCHAR(120) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        DefaultRecipientType NVARCHAR(80) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_NotificationEvents_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationEvents_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationEvents_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_NotificationEvents_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_NotificationEvents_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_NotificationEvents_Tenant_EventCode UNIQUE (TenantId, EventCode)
    );
END;
GO

IF OBJECT_ID(N'dbo.NotificationTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationTemplates
    (
        NotificationTemplateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationTemplates PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        NotificationEventId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Recipient NVARCHAR(120) NOT NULL,
        Subject NVARCHAR(300) NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        AllowedVariablesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_NotificationTemplates_AllowedVariablesJson DEFAULT N'[]',
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_NotificationTemplates_Status DEFAULT N'Active',
        UpdatedByUserId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationTemplates_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationTemplates_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_NotificationTemplates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_NotificationTemplates_NotificationEvents FOREIGN KEY (NotificationEventId) REFERENCES dbo.NotificationEvents (NotificationEventId),
        CONSTRAINT FK_NotificationTemplates_UpdatedByUser FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_NotificationTemplates_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT CK_NotificationTemplates_AllowedVariablesJson CHECK (ISJSON(AllowedVariablesJson) = 1),
        CONSTRAINT UQ_NotificationTemplates_Tenant_Event_Name UNIQUE (TenantId, NotificationEventId, Name)
    );
END;
GO

IF OBJECT_ID(N'dbo.NotificationOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationOutbox
    (
        NotificationOutboxId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationOutbox PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        NotificationEventId UNIQUEIDENTIFIER NOT NULL,
        NotificationTemplateId UNIQUEIDENTIFIER NULL,
        RecipientUserId UNIQUEIDENTIFIER NULL,
        RecipientEmail NVARCHAR(320) NULL,
        Channel NVARCHAR(20) NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_NotificationOutbox_PayloadJson DEFAULT N'{}',
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_NotificationOutbox_Status DEFAULT N'Pending',
        AttemptCount INT NOT NULL CONSTRAINT DF_NotificationOutbox_AttemptCount DEFAULT (0),
        AvailableAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationOutbox_AvailableAtUtc DEFAULT SYSUTCDATETIME(),
        ProcessedAtUtc DATETIME2(3) NULL,
        LastError NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationOutbox_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationOutbox_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_NotificationOutbox_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_NotificationOutbox_NotificationEvents FOREIGN KEY (NotificationEventId) REFERENCES dbo.NotificationEvents (NotificationEventId),
        CONSTRAINT FK_NotificationOutbox_NotificationTemplates FOREIGN KEY (NotificationTemplateId) REFERENCES dbo.NotificationTemplates (NotificationTemplateId),
        CONSTRAINT FK_NotificationOutbox_RecipientUser FOREIGN KEY (RecipientUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_NotificationOutbox_Channel CHECK (Channel IN (N'Email', N'SignalR')),
        CONSTRAINT CK_NotificationOutbox_Status CHECK (Status IN (N'Pending', N'Processing', N'Sent', N'Failed')),
        CONSTRAINT CK_NotificationOutbox_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        AuditLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        OccurredAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AuditLogs_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
        ActorUserId UNIQUEIDENTIFIER NULL,
        ActorDisplayName NVARCHAR(200) NOT NULL,
        EventType NVARCHAR(120) NOT NULL,
        EntityType NVARCHAR(120) NOT NULL,
        EntityId UNIQUEIDENTIFIER NULL,
        RecordLabel NVARCHAR(200) NOT NULL,
        EventSummary NVARCHAR(500) NOT NULL,
        Area NVARCHAR(120) NOT NULL,
        MetadataJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AuditLogs_MetadataJson DEFAULT N'{}',
        CONSTRAINT FK_AuditLogs_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AuditLogs_ActorUser FOREIGN KEY (ActorUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_AuditLogs_MetadataJson CHECK (ISJSON(MetadataJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.AiAgentDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiAgentDefinitions
    (
        AiAgentDefinitionId NVARCHAR(120) NOT NULL CONSTRAINT PK_AiAgentDefinitions PRIMARY KEY,
        DisplayName NVARCHAR(160) NOT NULL,
        Responsibility NVARCHAR(600) NOT NULL,
        InputSummary NVARCHAR(600) NOT NULL,
        OutputSummary NVARCHAR(600) NOT NULL,
        MvpBoundary NVARCHAR(600) NOT NULL,
        Enabled BIT NOT NULL CONSTRAINT DF_AiAgentDefinitions_Enabled DEFAULT (1),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAgentDefinitions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAgentDefinitions_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF OBJECT_ID(N'dbo.AiAgentRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiAgentRuns
    (
        AiAgentRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiAgentRuns PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AiAgentDefinitionId NVARCHAR(120) NOT NULL,
        SourceEntityType NVARCHAR(80) NOT NULL,
        SourceEntityId UNIQUEIDENTIFIER NOT NULL,
        ModelName NVARCHAR(120) NOT NULL,
        EmbeddingModelName NVARCHAR(120) NULL,
        InputHash NVARCHAR(128) NOT NULL,
        OutputSummary NVARCHAR(1000) NULL,
        Status NVARCHAR(20) NOT NULL,
        StartedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAgentRuns_StartedAtUtc DEFAULT SYSUTCDATETIME(),
        CompletedAtUtc DATETIME2(3) NULL,
        MetadataJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AiAgentRuns_MetadataJson DEFAULT N'{}',
        CONSTRAINT FK_AiAgentRuns_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AiAgentRuns_AiAgentDefinitions FOREIGN KEY (AiAgentDefinitionId) REFERENCES dbo.AiAgentDefinitions (AiAgentDefinitionId),
        CONSTRAINT CK_AiAgentRuns_Status CHECK (Status IN (N'Running', N'Succeeded', N'Failed', N'Skipped')),
        CONSTRAINT CK_AiAgentRuns_MetadataJson CHECK (ISJSON(MetadataJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.VectorEmbeddings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VectorEmbeddings
    (
        VectorEmbeddingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_VectorEmbeddings PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EntityType NVARCHAR(50) NOT NULL,
        EntityId UNIQUEIDENTIFIER NOT NULL,
        SourceType NVARCHAR(50) NOT NULL,
        SourceTextHash NVARCHAR(128) NOT NULL,
        EmbeddingModel NVARCHAR(100) NOT NULL,
        EmbeddingDimensions INT NOT NULL,
        Embedding VECTOR(768) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_VectorEmbeddings_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_VectorEmbeddings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NULL,
        CONSTRAINT FK_VectorEmbeddings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_VectorEmbeddings_EmbeddingDimensions CHECK (EmbeddingDimensions = 768)
    );
END;
GO

IF OBJECT_ID(N'dbo.KnowledgeChunks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeChunks
    (
        KnowledgeChunkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgeChunks PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ContextType NVARCHAR(80) NOT NULL,
        ContextEntityId UNIQUEIDENTIFIER NOT NULL,
        FocusEntityId UNIQUEIDENTIFIER NULL,
        SourceEntityType NVARCHAR(80) NOT NULL,
        SourceEntityId UNIQUEIDENTIFIER NOT NULL,
        SourceTitle NVARCHAR(240) NOT NULL,
        SourceRoute NVARCHAR(400) NULL,
        PermissionScope NVARCHAR(120) NOT NULL,
        Sensitivity NVARCHAR(40) NOT NULL,
        ChunkType NVARCHAR(80) NOT NULL,
        ChunkOrdinal INT NOT NULL,
        ChunkText NVARCHAR(MAX) NOT NULL,
        MetadataJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_KnowledgeChunks_MetadataJson DEFAULT N'{}',
        ContentHashSha256 NVARCHAR(128) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_KnowledgeChunks_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_KnowledgeChunks_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_KnowledgeChunks_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_KnowledgeChunks_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_KnowledgeChunks_MetadataJson CHECK (ISJSON(MetadataJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.AiAssistantConversations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiAssistantConversations
    (
        ConversationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiAssistantConversations PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        ContextType NVARCHAR(80) NOT NULL,
        ContextEntityId UNIQUEIDENTIFIER NOT NULL,
        FocusEntityId UNIQUEIDENTIFIER NULL,
        Title NVARCHAR(160) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_AiAssistantConversations_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAssistantConversations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAssistantConversations_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AiAssistantConversations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AiAssistantConversations_Users FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_AiAssistantConversations_Status CHECK (Status IN (N'Active', N'Archived'))
    );
END;
GO

IF OBJECT_ID(N'dbo.AiAssistantMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiAssistantMessages
    (
        MessageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiAssistantMessages PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ConversationId UNIQUEIDENTIFIER NOT NULL,
        Role NVARCHAR(20) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        ModelName NVARCHAR(120) NULL,
        AiAgentRunId UNIQUEIDENTIFIER NULL,
        PromptVersion NVARCHAR(80) NULL,
        ErrorCode NVARCHAR(120) NULL,
        ErrorMessage NVARCHAR(500) NULL,
        RetrievedChunkIdsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AiAssistantMessages_RetrievedChunkIdsJson DEFAULT N'[]',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAssistantMessages_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AiAssistantMessages_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AiAssistantMessages_Conversations FOREIGN KEY (ConversationId) REFERENCES dbo.AiAssistantConversations (ConversationId),
        CONSTRAINT FK_AiAssistantMessages_AgentRuns FOREIGN KEY (AiAgentRunId) REFERENCES dbo.AiAgentRuns (AiAgentRunId),
        CONSTRAINT CK_AiAssistantMessages_Role CHECK (Role IN (N'User', N'Assistant', N'System')),
        CONSTRAINT CK_AiAssistantMessages_RetrievedChunkIdsJson CHECK (ISJSON(RetrievedChunkIdsJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.AiAssistantMessageCitations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiAssistantMessageCitations
    (
        CitationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiAssistantMessageCitations PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        MessageId UNIQUEIDENTIFIER NOT NULL,
        KnowledgeChunkId UNIQUEIDENTIFIER NOT NULL,
        Label NVARCHAR(20) NOT NULL,
        SourceTitle NVARCHAR(240) NOT NULL,
        SourceType NVARCHAR(80) NOT NULL,
        SourceEntityId UNIQUEIDENTIFIER NOT NULL,
        SourceRoute NVARCHAR(400) NULL,
        Score DECIMAL(18, 6) NOT NULL,
        Excerpt NVARCHAR(700) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAssistantMessageCitations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AiAssistantMessageCitations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AiAssistantMessageCitations_Messages FOREIGN KEY (MessageId) REFERENCES dbo.AiAssistantMessages (MessageId),
        CONSTRAINT FK_AiAssistantMessageCitations_KnowledgeChunks FOREIGN KEY (KnowledgeChunkId) REFERENCES dbo.KnowledgeChunks (KnowledgeChunkId)
    );
END;
GO

IF OBJECT_ID(N'dbo.AiAssistantFeedback', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiAssistantFeedback
    (
        FeedbackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiAssistantFeedback PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        MessageId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Rating NVARCHAR(20) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAssistantFeedback_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiAssistantFeedback_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AiAssistantFeedback_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AiAssistantFeedback_Messages FOREIGN KEY (MessageId) REFERENCES dbo.AiAssistantMessages (MessageId),
        CONSTRAINT FK_AiAssistantFeedback_Users FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_AiAssistantFeedback_Rating CHECK (Rating IN (N'Helpful', N'NotHelpful'))
    );
END;
GO

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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUsers_Tenant_Status' AND object_id = OBJECT_ID(N'dbo.AppUsers'))
    CREATE INDEX IX_AppUsers_Tenant_Status ON dbo.AppUsers (TenantId, AccountStatus, DeletedAtUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUsers_Tenant_EmailNormalized' AND object_id = OBJECT_ID(N'dbo.AppUsers'))
    CREATE UNIQUE INDEX UX_AppUsers_Tenant_EmailNormalized ON dbo.AppUsers (TenantId, EmailNormalized) WHERE DeletedAtUtc IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRoles_Tenant_Role' AND object_id = OBJECT_ID(N'dbo.UserRoles'))
    CREATE INDEX IX_UserRoles_Tenant_Role ON dbo.UserRoles (TenantId, RoleId, UserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GroupMembers_Tenant_User' AND object_id = OBJECT_ID(N'dbo.GroupMembers'))
    CREATE INDEX IX_GroupMembers_Tenant_User ON dbo.GroupMembers (TenantId, UserId, GroupId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificationOutbox_Pending' AND object_id = OBJECT_ID(N'dbo.NotificationOutbox'))
    CREATE INDEX IX_NotificationOutbox_Pending ON dbo.NotificationOutbox (TenantId, Status, AvailableAtUtc) INCLUDE (Channel, AttemptCount);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Tenant_OccurredAt' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Tenant_OccurredAt ON dbo.AuditLogs (TenantId, OccurredAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Tenant_Entity' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Tenant_Entity ON dbo.AuditLogs (TenantId, EntityType, EntityId, OccurredAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAgentRuns_Tenant_Source' AND object_id = OBJECT_ID(N'dbo.AiAgentRuns'))
    CREATE INDEX IX_AiAgentRuns_Tenant_Source ON dbo.AiAgentRuns (TenantId, SourceEntityType, SourceEntityId, StartedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ExternalToolDailyUsage_Provider_Date' AND object_id = OBJECT_ID(N'dbo.ExternalToolDailyUsage'))
    CREATE UNIQUE INDEX UX_ExternalToolDailyUsage_Provider_Date ON dbo.ExternalToolDailyUsage (Provider, UsageDateUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VectorEmbeddings_Tenant_Entity' AND object_id = OBJECT_ID(N'dbo.VectorEmbeddings'))
    CREATE INDEX IX_VectorEmbeddings_Tenant_Entity ON dbo.VectorEmbeddings (TenantId, EntityType, EntityId, IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VectorEmbeddings_Model' AND object_id = OBJECT_ID(N'dbo.VectorEmbeddings'))
    CREATE INDEX IX_VectorEmbeddings_Model ON dbo.VectorEmbeddings (TenantId, EmbeddingModel, EmbeddingDimensions, IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_KnowledgeChunks_Context_Source' AND object_id = OBJECT_ID(N'dbo.KnowledgeChunks'))
    CREATE UNIQUE INDEX UX_KnowledgeChunks_Context_Source ON dbo.KnowledgeChunks (TenantId, ContextType, ContextEntityId, FocusEntityId, SourceEntityType, SourceEntityId, ChunkType, ChunkOrdinal);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KnowledgeChunks_Context_Active' AND object_id = OBJECT_ID(N'dbo.KnowledgeChunks'))
    CREATE INDEX IX_KnowledgeChunks_Context_Active ON dbo.KnowledgeChunks (TenantId, ContextType, ContextEntityId, FocusEntityId, IsActive) INCLUDE (KnowledgeChunkId, SourceEntityType, SourceEntityId, ChunkType);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAssistantConversations_User_Context' AND object_id = OBJECT_ID(N'dbo.AiAssistantConversations'))
    CREATE INDEX IX_AiAssistantConversations_User_Context ON dbo.AiAssistantConversations (TenantId, UserId, ContextType, ContextEntityId, FocusEntityId, UpdatedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAssistantMessages_Conversation' AND object_id = OBJECT_ID(N'dbo.AiAssistantMessages'))
    CREATE INDEX IX_AiAssistantMessages_Conversation ON dbo.AiAssistantMessages (TenantId, ConversationId, CreatedAtUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAssistantMessageCitations_Message' AND object_id = OBJECT_ID(N'dbo.AiAssistantMessageCitations'))
    CREATE INDEX IX_AiAssistantMessageCitations_Message ON dbo.AiAssistantMessageCitations (TenantId, MessageId, Label);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AiAssistantFeedback_Message_User' AND object_id = OBJECT_ID(N'dbo.AiAssistantFeedback'))
    CREATE UNIQUE INDEX UX_AiAssistantFeedback_Message_User ON dbo.AiAssistantFeedback (TenantId, MessageId, UserId);
GO
