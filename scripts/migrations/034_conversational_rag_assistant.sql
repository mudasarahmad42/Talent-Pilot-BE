-- Conversational RAG assistant MVP: chunk index, persisted conversations, citations, and feedback.

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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_KnowledgeChunks_Context_Source' AND object_id = OBJECT_ID(N'dbo.KnowledgeChunks'))
    CREATE UNIQUE INDEX UX_KnowledgeChunks_Context_Source
        ON dbo.KnowledgeChunks (TenantId, ContextType, ContextEntityId, FocusEntityId, SourceEntityType, SourceEntityId, ChunkType, ChunkOrdinal);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KnowledgeChunks_Context_Active' AND object_id = OBJECT_ID(N'dbo.KnowledgeChunks'))
    CREATE INDEX IX_KnowledgeChunks_Context_Active
        ON dbo.KnowledgeChunks (TenantId, ContextType, ContextEntityId, FocusEntityId, IsActive)
        INCLUDE (KnowledgeChunkId, SourceEntityType, SourceEntityId, ChunkType);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAssistantConversations_User_Context' AND object_id = OBJECT_ID(N'dbo.AiAssistantConversations'))
    CREATE INDEX IX_AiAssistantConversations_User_Context
        ON dbo.AiAssistantConversations (TenantId, UserId, ContextType, ContextEntityId, FocusEntityId, UpdatedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAssistantMessages_Conversation' AND object_id = OBJECT_ID(N'dbo.AiAssistantMessages'))
    CREATE INDEX IX_AiAssistantMessages_Conversation
        ON dbo.AiAssistantMessages (TenantId, ConversationId, CreatedAtUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiAssistantMessageCitations_Message' AND object_id = OBJECT_ID(N'dbo.AiAssistantMessageCitations'))
    CREATE INDEX IX_AiAssistantMessageCitations_Message
        ON dbo.AiAssistantMessageCitations (TenantId, MessageId, Label);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AiAssistantFeedback_Message_User' AND object_id = OBJECT_ID(N'dbo.AiAssistantFeedback'))
    CREATE UNIQUE INDEX UX_AiAssistantFeedback_Message_User
        ON dbo.AiAssistantFeedback (TenantId, MessageId, UserId);
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

MERGE dbo.Permissions AS target
USING (VALUES
    (N'ai.assistant.use', N'Use AI Assistant', N'AI', N'Ask evidence-grounded internal assistant questions in permitted workflow contexts.', N'Active')
) AS source (PermissionId, DisplayName, GroupName, Description, Status)
ON target.PermissionId = source.PermissionId
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = source.DisplayName,
        GroupName = source.GroupName,
        Description = source.Description,
        Status = source.Status,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (PermissionId, DisplayName, GroupName, Description, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.PermissionId, source.DisplayName, source.GroupName, source.Description, source.Status, @Now, @Now);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAtUtc)
SELECT roleSource.RoleId, N'ai.assistant.use', @Now
FROM dbo.Roles AS roleSource
WHERE roleSource.Code IN (N'SystemAdmin', N'TenantAdmin', N'PMO', N'Recruiter', N'HiringManager')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RolePermissions AS existing
      WHERE existing.RoleId = roleSource.RoleId
        AND existing.PermissionId = N'ai.assistant.use'
  );

MERGE dbo.AiAgentDefinitions AS target
USING (VALUES
    (N'conversational-rag-assistant', N'Conversational RAG Assistant', N'Answers internal Talent Pilot questions by retrieving permitted knowledge chunks and generating cited, read-only summaries.', N'User question, assistant context, recent conversation history, permitted knowledge chunks, citation labels, and workflow visibility metadata.', N'Evidence-grounded answer with citations, prompt version, retrieved chunk ids, model, and agent run metadata.', N'Read-only. It cannot approve, reject, hire, allocate, move workflow stages, schedule meetings, generate offers, or contact users.', CAST(1 AS BIT))
) AS source (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled)
ON target.AiAgentDefinitionId = source.AiAgentDefinitionId
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = source.DisplayName,
        Responsibility = source.Responsibility,
        InputSummary = source.InputSummary,
        OutputSummary = source.OutputSummary,
        MvpBoundary = source.MvpBoundary,
        Enabled = source.Enabled,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (AiAgentDefinitionId, DisplayName, Responsibility, InputSummary, OutputSummary, MvpBoundary, Enabled, CreatedAtUtc, UpdatedAtUtc)
    VALUES (source.AiAgentDefinitionId, source.DisplayName, source.Responsibility, source.InputSummary, source.OutputSummary, source.MvpBoundary, source.Enabled, @Now, @Now);
GO
