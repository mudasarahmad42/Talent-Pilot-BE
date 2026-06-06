-- 041_add_candidate_profile_documents.sql
-- Stores candidate profile resume/CV metadata separately from per-application
-- documents. File bytes remain behind the application document storage provider.

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.CandidateProfileDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateProfileDocuments
    (
        CandidateProfileDocumentId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_CandidateProfileDocuments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        DocumentType NVARCHAR(64) NOT NULL,
        OriginalFileName NVARCHAR(260) NOT NULL,
        ContentType NVARCHAR(160) NOT NULL,
        SizeBytes BIGINT NOT NULL,
        StorageProvider NVARCHAR(64) NOT NULL,
        StorageKey NVARCHAR(512) NOT NULL,
        StorageContainer NVARCHAR(128) NULL,
        ContentHashSha256 CHAR(64) NOT NULL,
        ExtractionStatus NVARCHAR(32) NOT NULL
            CONSTRAINT DF_CandidateProfileDocuments_ExtractionStatus DEFAULT N'Pending',
        ExtractedText NVARCHAR(MAX) NULL,
        ExtractedTextHashSha256 CHAR(64) NULL,
        ParserVersion NVARCHAR(64) NULL,
        ExtractedAtUtc DATETIME2(7) NULL,
        ExtractionError NVARCHAR(1000) NULL,
        Status NVARCHAR(32) NOT NULL
            CONSTRAINT DF_CandidateProfileDocuments_Status DEFAULT N'Active',
        UploadedByUserId UNIQUEIDENTIFIER NULL,
        UploadedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_CandidateProfileDocuments_UploadedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_CandidateProfileDocuments_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_CandidateProfileDocuments_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateProfileDocuments_Tenants
            FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
        CONSTRAINT FK_CandidateProfileDocuments_Candidates
            FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates(CandidateId),
        CONSTRAINT FK_CandidateProfileDocuments_UploadedBy
            FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUsers(UserId),
        CONSTRAINT CK_CandidateProfileDocuments_SizeBytes_Positive
            CHECK (SizeBytes > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_CandidateProfileDocuments_Tenant_Candidate_Status'
      AND object_id = OBJECT_ID(N'dbo.CandidateProfileDocuments')
)
BEGIN
    CREATE INDEX IX_CandidateProfileDocuments_Tenant_Candidate_Status
        ON dbo.CandidateProfileDocuments (TenantId, CandidateId, Status, UploadedAtUtc DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_CandidateProfileDocuments_Tenant_Document_Extraction'
      AND object_id = OBJECT_ID(N'dbo.CandidateProfileDocuments')
)
BEGIN
    CREATE INDEX IX_CandidateProfileDocuments_Tenant_Document_Extraction
        ON dbo.CandidateProfileDocuments (TenantId, CandidateProfileDocumentId, ExtractionStatus);
END;
GO
