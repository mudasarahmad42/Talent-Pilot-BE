-- 012_add_recruiter_job_posts.sql
-- Adds first-class recruiter-owned Job Posts for the Recruiter Sourcing slice.
-- The script is additive and safe to rerun.

IF OBJECT_ID(N'dbo.JobPosts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobPosts
    (
        JobPostId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobPosts PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        RecruiterOwnerUserId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        LocationId UNIQUEIDENTIFIER NULL,
        ExperienceMinYears DECIMAL(4,1) NULL,
        ExperienceMaxYears DECIMAL(4,1) NULL,
        RequiredPositions INT NOT NULL CONSTRAINT DF_JobPosts_RequiredPositions DEFAULT (1),
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_JobPosts_Status DEFAULT N'Draft',
        PublishedAtUtc DATETIME2(3) NULL,
        ClosedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobPosts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobPosts_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_JobPosts_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobPosts_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_JobPosts_RecruiterOwner FOREIGN KEY (RecruiterOwnerUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_JobPosts_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
        CONSTRAINT FK_JobPosts_Locations FOREIGN KEY (LocationId) REFERENCES dbo.Locations (LocationId),
        CONSTRAINT CK_JobPosts_Positions CHECK (RequiredPositions > 0),
        CONSTRAINT CK_JobPosts_Status CHECK (Status IN (N'Draft', N'Published', N'Closed')),
        CONSTRAINT UQ_JobPosts_Tenant_JobRequest UNIQUE (TenantId, JobRequestId)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobPostSkills', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobPostSkills
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobPostId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_JobPostSkills_IsRequired DEFAULT (1),
        Weight INT NOT NULL CONSTRAINT DF_JobPostSkills_Weight DEFAULT (1),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobPostSkills_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_JobPostSkills PRIMARY KEY (TenantId, JobPostId, SkillId),
        CONSTRAINT FK_JobPostSkills_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobPostSkills_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId),
        CONSTRAINT FK_JobPostSkills_Skills FOREIGN KEY (SkillId) REFERENCES dbo.Skills (SkillId),
        CONSTRAINT CK_JobPostSkills_Weight CHECK (Weight BETWEEN 1 AND 10)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobPostInterviewRounds', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobPostInterviewRounds
    (
        JobPostInterviewRoundId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobPostInterviewRounds PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobPostId UNIQUEIDENTIFIER NOT NULL,
        InterviewTemplateRoundId UNIQUEIDENTIFIER NULL,
        RoundOrder INT NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        OwnerUserId UNIQUEIDENTIFIER NULL,
        DurationMinutes INT NOT NULL CONSTRAINT DF_JobPostInterviewRounds_Duration DEFAULT (60),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_JobPostInterviewRounds_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobPostInterviewRounds_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobPostInterviewRounds_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JobPostInterviewRounds_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobPostInterviewRounds_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId),
        CONSTRAINT FK_JobPostInterviewRounds_TemplateRounds FOREIGN KEY (InterviewTemplateRoundId) REFERENCES dbo.InterviewTemplateRounds (InterviewTemplateRoundId),
        CONSTRAINT FK_JobPostInterviewRounds_OwnerUser FOREIGN KEY (OwnerUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_JobPostInterviewRounds_Duration CHECK (DurationMinutes BETWEEN 15 AND 240),
        CONSTRAINT CK_JobPostInterviewRounds_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_JobPostInterviewRounds_Post_Order UNIQUE (JobPostId, RoundOrder)
    );
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'JobPostId') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplications ADD JobPostId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplications', N'JobPostId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_JobApplications_JobPosts'
          AND parent_object_id = OBJECT_ID(N'dbo.JobApplications')
   )
BEGIN
    ALTER TABLE dbo.JobApplications
    ADD CONSTRAINT FK_JobApplications_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId);
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_JobRequests_PublishStatus'
      AND parent_object_id = OBJECT_ID(N'dbo.JobRequests')
)
BEGIN
    ALTER TABLE dbo.JobRequests DROP CONSTRAINT CK_JobRequests_PublishStatus;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_JobRequests_PublishStatus'
      AND parent_object_id = OBJECT_ID(N'dbo.JobRequests')
)
BEGIN
    ALTER TABLE dbo.JobRequests
    ADD CONSTRAINT CK_JobRequests_PublishStatus CHECK (PublishStatus IN (N'NotPublished', N'Published', N'Unpublished', N'Closed'));
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobPosts_Tenant_Status' AND object_id = OBJECT_ID(N'dbo.JobPosts'))
    CREATE INDEX IX_JobPosts_Tenant_Status ON dbo.JobPosts (TenantId, Status, UpdatedAtUtc DESC) INCLUDE (JobRequestId, RecruiterOwnerUserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobPostInterviewRounds_Post_Status' AND object_id = OBJECT_ID(N'dbo.JobPostInterviewRounds'))
    CREATE INDEX IX_JobPostInterviewRounds_Post_Status ON dbo.JobPostInterviewRounds (TenantId, JobPostId, Status, RoundOrder);
GO
