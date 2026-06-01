-- 015_job_portal_applications_manual_sourcing.sql
-- Adds candidate portal application metadata, manual sourcing linkage, and analytics-ready candidate history tables.

IF COL_LENGTH(N'dbo.JobApplications', N'SourceDetail') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications ADD SourceDetail NVARCHAR(200) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'SourceUrl') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications ADD SourceUrl NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'AddedByUserId') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications ADD AddedByUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'RecruiterNotes') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications ADD RecruiterNotes NVARCHAR(1000) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'ApplicationSnapshotJson') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications ADD ApplicationSnapshotJson NVARCHAR(MAX) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'AddedByUserId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_JobApplications_AddedByUser'
          AND parent_object_id = OBJECT_ID(N'dbo.JobApplications')
   )
BEGIN
    ALTER TABLE dbo.JobApplications
    ADD CONSTRAINT FK_JobApplications_AddedByUser FOREIGN KEY (AddedByUserId) REFERENCES dbo.AppUsers (UserId);
END;
GO

IF COL_LENGTH(N'dbo.CandidateInvitations', N'JobPostId') IS NULL
BEGIN
    ALTER TABLE dbo.CandidateInvitations ADD JobPostId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.CandidateInvitations', N'JobPostId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_CandidateInvitations_JobPosts'
          AND parent_object_id = OBJECT_ID(N'dbo.CandidateInvitations')
   )
BEGIN
    ALTER TABLE dbo.CandidateInvitations
    ADD CONSTRAINT FK_CandidateInvitations_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId);
END;
GO

IF COL_LENGTH(N'dbo.CandidateProspectJobRequests', N'JobPostId') IS NULL
BEGIN
    ALTER TABLE dbo.CandidateProspectJobRequests ADD JobPostId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.CandidateProspectJobRequests', N'JobPostId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_CandidateProspectJobRequests_JobPosts'
          AND parent_object_id = OBJECT_ID(N'dbo.CandidateProspectJobRequests')
   )
BEGIN
    ALTER TABLE dbo.CandidateProspectJobRequests
    ADD CONSTRAINT FK_CandidateProspectJobRequests_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId);
END;
GO

IF OBJECT_ID(N'dbo.CandidateEducation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateEducation
    (
        CandidateEducationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateEducation PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        UniversityName NVARCHAR(200) NOT NULL,
        DegreeName NVARCHAR(200) NULL,
        GraduationYear INT NULL,
        IsPrimary BIT NOT NULL CONSTRAINT DF_CandidateEducation_IsPrimary DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateEducation_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateEducation_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateEducation_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateEducation_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT CK_CandidateEducation_GraduationYear CHECK (GraduationYear IS NULL OR GraduationYear BETWEEN 1950 AND 2100)
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateWorkHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateWorkHistory
    (
        CandidateWorkHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateWorkHistory PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        CompanyName NVARCHAR(200) NOT NULL,
        Title NVARCHAR(200) NULL,
        IsCurrent BIT NOT NULL CONSTRAINT DF_CandidateWorkHistory_IsCurrent DEFAULT (0),
        StartsOn DATE NULL,
        EndsOn DATE NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateWorkHistory_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateWorkHistory_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateWorkHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateWorkHistory_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CandidateEducation_Candidate' AND object_id = OBJECT_ID(N'dbo.CandidateEducation'))
    CREATE INDEX IX_CandidateEducation_Candidate ON dbo.CandidateEducation (TenantId, CandidateId, IsPrimary DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CandidateWorkHistory_Candidate' AND object_id = OBJECT_ID(N'dbo.CandidateWorkHistory'))
    CREATE INDEX IX_CandidateWorkHistory_Candidate ON dbo.CandidateWorkHistory (TenantId, CandidateId, IsCurrent DESC);
GO

INSERT INTO dbo.CandidateSourceLabels
(
    CandidateSourceLabelId,
    TenantId,
    Code,
    DisplayName,
    ReportingCategory,
    Status,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    NEWID(),
    tenant.TenantId,
    N'JobPortal',
    N'Job Portal',
    N'Talent Pilot portal',
    N'Active',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM dbo.Tenants AS tenant
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.CandidateSourceLabels AS label
    WHERE label.TenantId = tenant.TenantId
      AND label.Code = N'JobPortal'
);
GO
