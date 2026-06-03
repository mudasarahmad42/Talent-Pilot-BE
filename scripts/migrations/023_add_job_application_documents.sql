-- 023_add_job_application_documents.sql
-- Stores candidate-submitted application document metadata while file bytes remain
-- behind the application document storage provider. MVP provider is local server
-- filesystem; Azure Blob can replace it by writing the same metadata contract.

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.JobApplicationDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobApplicationDocuments
    (
        ApplicationDocumentId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_JobApplicationDocuments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        DocumentType NVARCHAR(64) NOT NULL,
        OriginalFileName NVARCHAR(260) NOT NULL,
        ContentType NVARCHAR(160) NOT NULL,
        SizeBytes BIGINT NOT NULL,
        StorageProvider NVARCHAR(64) NOT NULL,
        StorageKey NVARCHAR(512) NOT NULL,
        StorageContainer NVARCHAR(128) NULL,
        ContentHashSha256 CHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL
            CONSTRAINT DF_JobApplicationDocuments_Status DEFAULT N'Active',
        UploadedByUserId UNIQUEIDENTIFIER NULL,
        UploadedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_JobApplicationDocuments_UploadedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_JobApplicationDocuments_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_JobApplicationDocuments_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JobApplicationDocuments_Tenants
            FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(TenantId),
        CONSTRAINT FK_JobApplicationDocuments_JobApplications
            FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications(JobApplicationId),
        CONSTRAINT FK_JobApplicationDocuments_Candidates
            FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates(CandidateId),
        CONSTRAINT FK_JobApplicationDocuments_UploadedBy
            FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUsers(UserId),
        CONSTRAINT CK_JobApplicationDocuments_SizeBytes_Positive
            CHECK (SizeBytes > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_JobApplicationDocuments_Tenant_Application_Status'
      AND object_id = OBJECT_ID(N'dbo.JobApplicationDocuments')
)
BEGIN
    CREATE INDEX IX_JobApplicationDocuments_Tenant_Application_Status
        ON dbo.JobApplicationDocuments (TenantId, JobApplicationId, Status, UploadedAtUtc DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_JobApplicationDocuments_Tenant_Candidate'
      AND object_id = OBJECT_ID(N'dbo.JobApplicationDocuments')
)
BEGIN
    CREATE INDEX IX_JobApplicationDocuments_Tenant_Candidate
        ON dbo.JobApplicationDocuments (TenantId, CandidateId, UploadedAtUtc DESC);
END;
GO
